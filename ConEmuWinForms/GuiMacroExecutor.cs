using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ConEmu.WinForms.Util;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace ConEmu.WinForms
{
	/// <summary>
	///     <para>Implements calling GuiMacro to the remote ConEmu instance, and getting the result.</para>
	///     <para>Got switching implementation for out-of-process (classic, via a console tool) and in-process (new feature which loads the helper comm DLL directly) access.</para>
	/// </summary>
	public unsafe class GuiMacroExecutor : IDisposable
	{
		[CanBeNull]
		private FGuiMacro _fnGuiMacro;

		[CanBeNull]
		private void* _hConEmuCD;

		/// <summary>
		/// Prevent unloads when async calls are being placed.
		/// </summary>
		private readonly object _lock = new object();

		/// <summary>
		/// Inits the object, loads the extender DLL if known. If <c>NULL</c>, in-process operations will not be available.
		/// </summary>
		public GuiMacroExecutor([CanBeNull] string asLibrary)
		{
			if(!String.IsNullOrWhiteSpace(asLibrary))
				LoadConEmuDll(asLibrary);
		}

		/// <summary>
		///     <para>Loads the ConEmu Console Server DLL and uses it to execute the GUI Macro.</para>
		///     <para>The execution is async, because the call must not be placed on the same thread.</para>
		/// </summary>
		[NotNull]
		public Task<GuiMacroResult> ExecuteInProcessAsync(int nConEmuPid, [NotNull] string asMacro)
		{
			if(asMacro == null)
				throw new ArgumentNullException(nameof(asMacro));
			if(_hConEmuCD == null) // Check on home thread
				throw new GuiMacroException("ConEmuCD was not loaded.");

			// Bring the call on another thread, because placing the call on the same thread as ConEmu might cause a deadlock when it's still in the process of initialization
			// (the GuiMacro stuff was designed for out-of-process comm and would blocking-wait for init to complete)
			return Task.Run(() =>
			{
				lock(_lock) // Don't allow unloading in parallel
				{
					if(_hConEmuCD == null) // Re-check after lock-protecting from unload
						throw new GuiMacroException("ConEmuCD has just been unloaded.");
					if(_fnGuiMacro == null)
						throw new GuiMacroException("The function pointer has not been bound.");

					string sResult;
					int iRc = _fnGuiMacro(nConEmuPid.ToString(CultureInfo.InvariantCulture), asMacro, out sResult);
					switch(iRc)
					{
					case 0: // This is expected
					case 133: // CERR_GUIMACRO_SUCCEEDED: not expected, but...
						return new GuiMacroResult() {IsSuccessful = true, Response = sResult ?? ""};
					case 134: // CERR_GUIMACRO_FAILED
						return new GuiMacroResult() {IsSuccessful = false};
					default:
						throw new GuiMacroException($"Internal ConEmuCD error: {iRc:N0}.");
					}
				}
			});
		}

		/// <summary>
		/// Invokes <c>ConEmuC.exe</c> to execute the GUI Macro.
		/// The execution is asynchronous.
		/// </summary>
		[NotNull]
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
				Task<GuiMacroResult> taskStart = Task.Run(() =>
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

				return taskStart;
			}
			catch(Exception ex)
			{
				throw new InvalidOperationException($"Could not run the ConEmu Console Extender Executable at “{sConEmuConsoleExtenderExecutablePath}” with command-line arguments “{cmdl}”.", ex);
			}
		}

		void IDisposable.Dispose()
		{
			lock(_lock)
				UnloadConEmuDll();
			GC.SuppressFinalize(this);
		}

		~GuiMacroExecutor()
		{
			// Locking: don't take lock in the finalizer, could do nasty things, supposedly, “this” is reachable when background tasks are running
			UnloadConEmuDll();
		}

		private void LoadConEmuDll([NotNull] string asLibrary)
		{
			if(asLibrary == null)
				throw new ArgumentNullException(nameof(asLibrary));
			if(_hConEmuCD != null)
				return;

			_hConEmuCD = WinApi.LoadLibrary(asLibrary);
			if(_hConEmuCD == null)
			{
				int errorCode = Marshal.GetLastWin32Error();
				throw new GuiMacroException($"Can't load library, ErrCode={errorCode}\n{asLibrary}");
			}

			const string fnName = "GuiMacro";
			void* exportPtr = WinApi.GetProcAddress(_hConEmuCD, fnName);
			if(exportPtr == null)
			{
				UnloadConEmuDll();
				throw new GuiMacroException($"Function {fnName} not found in library\n{asLibrary}\nUpdate ConEmu modules");
			}
			_fnGuiMacro = (FGuiMacro)Marshal.GetDelegateForFunctionPointer((IntPtr)exportPtr, typeof(FGuiMacro));
		}

		private void UnloadConEmuDll()
		{
			if(_hConEmuCD != null)
			{
				WinApi.FreeLibrary(_hConEmuCD);
				_hConEmuCD = null;
			}
		}

		/// <summary>
		///     <code>int __stdcall GuiMacro(LPCWSTR asInstance, LPCWSTR asMacro, BSTR* bsResult = NULL);</code>
		/// </summary>
		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate int FGuiMacro([MarshalAs(UnmanagedType.LPWStr)] string asInstance, [MarshalAs(UnmanagedType.LPWStr)] string asMacro, [MarshalAs(UnmanagedType.BStr)] out string bsResult);

		public class GuiMacroException : Exception
		{
			public GuiMacroException(string asMessage)
				: base(asMessage)
			{
			}
		}
	}
}