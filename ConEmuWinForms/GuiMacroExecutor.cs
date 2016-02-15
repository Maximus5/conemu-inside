using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace ConEmu.WinForms
{
	public unsafe class GuiMacroExecutor : IDisposable
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern void* LoadLibrary(string libname);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern bool FreeLibrary(void* hModule);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern void* GetProcAddress(void* hModule, string lpProcName);

		/// <summary>
		///     <code>int __stdcall GuiMacro(LPCWSTR asInstance, LPCWSTR asMacro, GuiMacroResultCallback ResultCallback = NULL);            </code>
		/// </summary>
		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate int FGuiMacro(string asWhere, string asMacro, IntPtr ExecuteResultDelegate);

		/// <summary>
		///     <code>typedef void (__stdcall* GuiMacroResultCallback)(GuiMacroResult code, LPCWSTR result);</code>
		/// </summary>
		private delegate void FGuiMacroResultCallback(int code, [MarshalAs(UnmanagedType.LPWStr)] [NotNull] string result);

		private readonly string libraryPath;

		private void* hConEmuCD;

		private FGuiMacro fnGuiMacro;

		public string LibraryPath => libraryPath;

		/// <summary>
		/// Loads the ConEmu Console Server DLL and uses it to execute the GUI Macro, synchronously.
		/// </summary>
		/// <param name="nConEmuPid"></param>
		/// <param name="asMacro"></param>
		/// <returns></returns>
		public Task<GuiMacroResult> ExecuteInProcessAsync(int nConEmuPid, [NotNull] string asMacro)
		{
			if(asMacro == null)
				throw new ArgumentNullException(nameof(asMacro));

			if(hConEmuCD == null)
				throw new GuiMacroException("ConEmuCD was not loaded.");

			// Bring the call on another thread
			// It creates a task which completes when the callback arrives
			// This would work with any assumptions on the callback synchronousness, just make sure it's ever called
			FGuiMacroResultCallback fnCallback = null;
			Task<Task<GuiMacroResult>> taskInitiateCall = Task.Factory.StartNew(() =>
			{
				var taskresult = new TaskCompletionSource<GuiMacroResult>();

				fnCallback = (nCode, sResponseText) => taskresult.SetResult(new GuiMacroResult() {Response = sResponseText, IsSuccessful = nCode == 0});

				int iRc = fnGuiMacro(nConEmuPid.ToString(CultureInfo.InvariantCulture), asMacro, Marshal.GetFunctionPointerForDelegate(fnCallback));
				switch(iRc)
				{
				case 0: // This is expected
				case 133: // CERR_GUIMACRO_SUCCEEDED: not expected, but...
					// OK
					break;
				case 134: // CERR_GUIMACRO_FAILED
					taskresult.SetResult(new GuiMacroResult() {IsSuccessful = false});
					break;
				default:
					throw new GuiMacroException($"Internal ConEmuCD error: {iRc:N0}.");
				}

				// Keep the callback alive for now
				GC.KeepAlive(fnCallback);

				return taskresult.Task;
			});

			// And this waits for the resulting task to arrive
			Task<GuiMacroResult> taskResult = taskInitiateCall.Unwrap();

			// Make sure the native thunk we pass for callback is not reclaimed too soon
			Task<GuiMacroResult> taskResultAndKeepalive = taskResult.ContinueWith(task =>
			{
				// This brings the managed fn (its thunk lifetime is bound to it) into the closure and keeps it alive up until this point, which is after the callback execution
				GC.KeepAlive(fnCallback);

				// Result passthru
				return task.Result;
			});

			return taskResultAndKeepalive;
		}

		/// <summary>
		/// Inits the object, loads the extender DLL if known. If <c>NULL</c>, in-process operations will not be available.
		/// </summary>
		/// <param name="asLibrary"></param>
		public GuiMacroExecutor([CanBeNull] string asLibrary)
		{
			libraryPath = asLibrary;
			if(!string.IsNullOrEmpty(asLibrary))
				LoadConEmuDll(asLibrary);
		}

		~GuiMacroExecutor()
		{
			UnloadConEmuDll();
		}

		void IDisposable.Dispose()
		{
			UnloadConEmuDll();
			GC.SuppressFinalize(this);
		}

		private void LoadConEmuDll([NotNull] string asLibrary)
		{
			if(asLibrary == null)
				throw new ArgumentNullException(nameof(asLibrary));
			if(hConEmuCD != null)
				return;

			hConEmuCD = LoadLibrary(asLibrary);
			if(hConEmuCD == null)
			{
				int errorCode = Marshal.GetLastWin32Error();
				throw new GuiMacroException($"Can't load library, ErrCode={errorCode}\n{asLibrary}");
			}

			const string fnName = "GuiMacro";
			void* exportPtr = GetProcAddress(hConEmuCD, fnName);
			if(exportPtr == null)
			{
				UnloadConEmuDll();
				throw new GuiMacroException($"Function {fnName} not found in library\n{asLibrary}\nUpdate ConEmu modules");
			}
			fnGuiMacro = (FGuiMacro)Marshal.GetDelegateForFunctionPointer((IntPtr)exportPtr, typeof(FGuiMacro));
		}

		private void UnloadConEmuDll()
		{
			if(hConEmuCD != null)
			{
				FreeLibrary(hConEmuCD);
				hConEmuCD = null;
			}
		}

		/// <summary>
		/// Invokes <c>ConEmuC.exe</c> to execute the GUI Macro.
		/// </summary>
		public Task<GuiMacroResult> ExecuteViaExtenderProcessAsync([NotNull] string macrotext, int nConEmuPid, [NotNull] string sConEmuConsoleExtenderExecutablePath)
		{
			if(macrotext == null)
				throw new ArgumentNullException(nameof(macrotext));
			if(sConEmuConsoleExtenderExecutablePath == null)
				throw new ArgumentNullException(nameof(sConEmuConsoleExtenderExecutablePath));

			// conemuc.exe -silent -guimacro:1234 print("\e","git"," --version","\n")
			var cmdl = new CommandLineBuilder();
			cmdl.AppendSwitch("-silent");
			cmdl.AppendSwitchIfNotNull("-GuiMacro:", nConEmuPid.ToString());
			cmdl.AppendSwitch(macrotext /* appends the text unquoted for cmdline */);

			if(sConEmuConsoleExtenderExecutablePath == "")
				throw new InvalidOperationException("The ConEmu Console Extender Executable is not available.");
			if(!File.Exists(sConEmuConsoleExtenderExecutablePath))
				throw new InvalidOperationException($"The ConEmu Console Extender Executable does not exist on disk at “{sConEmuConsoleExtenderExecutablePath}”.");

			try
			{
				Task<Task<GuiMacroResult>> taskStart = Task.Factory.StartNew(() =>
				{
					var processExtender = new Process() {StartInfo = new ProcessStartInfo(sConEmuConsoleExtenderExecutablePath, cmdl.ToString()) {WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false}};

					processExtender.EnableRaisingEvents = true;
					var sbResult = new StringBuilder();
					DataReceivedEventHandler FOnData = (sender, args) =>
					{
						lock(sbResult)
							sbResult.Append(args.Data);
					};
					processExtender.OutputDataReceived += FOnData;
					processExtender.ErrorDataReceived += FOnData;

					var taskresult = new TaskCompletionSource<GuiMacroResult>();
					processExtender.Exited += delegate
					{
						GuiMacroResult result;
						lock(sbResult)
							result = new GuiMacroResult() {IsSuccessful = processExtender.ExitCode == 0, Response = sbResult.ToString()};
						taskresult.SetResult(result);
					};

					processExtender.Start();

					processExtender.BeginOutputReadLine();
					processExtender.BeginErrorReadLine();

					return taskresult.Task;
				});

				return taskStart.Unwrap();
			}
			catch(Exception ex)
			{
				throw new InvalidOperationException($"Could not run the ConEmu Console Extender Executable at “{sConEmuConsoleExtenderExecutablePath}” with command-line arguments “{cmdl}”.", ex);
			}
		}

		public class GuiMacroException : Exception
		{
			public GuiMacroException(string asMessage)
				: base(asMessage)
			{
			}
		}
	}
}
