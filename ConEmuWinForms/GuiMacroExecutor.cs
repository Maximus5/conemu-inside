using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace ConEmu.WinForms
{

    public class GuiMacroExecutor : IDisposable
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int CConsoleMain3(int anWorkMode, string asCommandLine);

        private string libraryPath;
        private IntPtr ConEmuCD;
        private CConsoleMain3 ConsoleMain3;

        public string LibraryPath
        {
            get
            {
                return libraryPath;
            }
        }

	    /// <summary>
	    /// Loads the ConEmu Console Server DLL and uses it to execute the GUI Macro, synchronously.
	    /// </summary>
	    /// <param name="nConEmuPid"></param>
	    /// <param name="asMacro"></param>
	    /// <returns></returns>
	    public GuiMacroResult ExecuteInProcess(int nConEmuPid, [NotNull] string asMacro)
        {
	        if(asMacro == null)
		        throw new ArgumentNullException(nameof(asMacro));

	        if (ConEmuCD == IntPtr.Zero)
            {
                throw new GuiMacroException("ConEmuCD was not loaded");
            }

            string cmdLine = " -GuiMacro";
            cmdLine += ":" + nConEmuPid;
            cmdLine += " " + asMacro;

            Environment.SetEnvironmentVariable("ConEmuMacroResult", null);

            GuiMacroResult result;
            int iRc = ConsoleMain3.Invoke(3, cmdLine);
            switch (iRc)
            {
                case 200: // CERR_CMDLINEEMPTY
                case 201: // CERR_CMDLINE
                    throw new GuiMacroException("Bad command line was passed to ConEmuCD.");
                case 0: // This is expected
                case 133: // CERR_GUIMACRO_SUCCEEDED: not expected, but...
                    var resulttext = Environment.GetEnvironmentVariable("ConEmuMacroResult");
                    if (resulttext == null)
                        throw new GuiMacroException("ConEmuMacroResult was not set.");
					result = new GuiMacroResult() {ErrorLevel = 0, Response = resulttext};
                    break;
                case 134: // CERR_GUIMACRO_FAILED
                    result= new GuiMacroResult() {ErrorLevel = iRc};
					break;
                default:
                    throw new GuiMacroException($"Internal ConEmuCD error: {iRc:N0}.");
            }

            return result;
        }

		/// <summary>
		/// Inits the object, loads the extender DLL if known. If <c>NULL</c>, in-process operations will not be available.
		/// </summary>
		/// <param name="asLibrary"></param>
        public GuiMacroExecutor([CanBeNull] string asLibrary)
        {
            ConEmuCD = IntPtr.Zero;
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
	        if (ConEmuCD != IntPtr.Zero)
            {
                return;
            }

            ConEmuCD = LoadLibrary(asLibrary);
            if (ConEmuCD == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new GuiMacroException($"Can't load library, ErrCode={errorCode}\n{asLibrary}");
            }

            // int __stdcall ConsoleMain3(int anWorkMode/*0-Server&ComSpec,1-AltServer,2-Reserved*/, LPCWSTR asCmdLine)
            const string fnName = "ConsoleMain3";
            IntPtr exportPtr = GetProcAddress(ConEmuCD, fnName);
            if (exportPtr == IntPtr.Zero)
            {
                UnloadConEmuDll();
                throw new GuiMacroException($"Function {fnName} not found in library\n{asLibrary}\nUpdate ConEmu modules");
            }
            ConsoleMain3 = (CConsoleMain3)Marshal.GetDelegateForFunctionPointer(exportPtr, typeof(CConsoleMain3));
            // To call: ConsoleMain3.Invoke(0, cmdline);
        }

        private void UnloadConEmuDll()
        {
            if (ConEmuCD != IntPtr.Zero)
            {
                FreeLibrary(ConEmuCD);
                ConEmuCD = IntPtr.Zero;
            }
        }

		/// <summary>
		/// Invokes <c>ConEmuC.exe</c> to execute the GUI Macro.
		/// </summary>
		public void ExecuteViaExtenderProcess([NotNull] string macrotext, [CanBeNull] Action<GuiMacroResult> FWhenDone, bool isSync, int nConEmuPid, [NotNull] string sConEmuConsoleExtenderExecutablePath)
		{
			if(macrotext == null)
				throw new ArgumentNullException(nameof(macrotext));
			if(sConEmuConsoleExtenderExecutablePath == null)
				throw new ArgumentNullException(nameof(sConEmuConsoleExtenderExecutablePath));

			SynchronizationContext dispatcher = SynchronizationContext.Current; // Home thread for callbacks

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
				var processExtender = new Process() {StartInfo = new ProcessStartInfo(sConEmuConsoleExtenderExecutablePath, cmdl.ToString()) {WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false}};
				Action FFireResult = null;

				if(FWhenDone != null)
				{
					var sbResult = new StringBuilder();

					// A func to fire result — WaitForExit does not guarantee that Exited event will fire, so gotta call it manually just in case, and make sure it's called only once
					FFireResult = () =>
					{
						GuiMacroResult result;
						lock(sbResult)
							result = new GuiMacroResult() {ErrorLevel = processExtender.ExitCode, Response = sbResult.ToString()};

						if(isSync) // Means on the home thread already
							FWhenDone(result);
						else
							dispatcher.Post(delegate { FWhenDone(result); }, null);
					};

					processExtender.EnableRaisingEvents = true;
#pragma warning disable once AccessToModifiedClosure
					if(!isSync)
						processExtender.Exited += delegate { Interlocked.Exchange(ref FFireResult, null)?.Invoke(); };
					DataReceivedEventHandler FOnData = (sender, args) =>
					{
						lock(sbResult)
							sbResult.Append(args.Data);
					};
					processExtender.OutputDataReceived += FOnData;
					processExtender.ErrorDataReceived += FOnData;
				}

				processExtender.Start();

				if(FWhenDone != null)
				{
					processExtender.BeginOutputReadLine();
					processExtender.BeginErrorReadLine();
				}

				// Wait until done if sync
				if(isSync)
				{
					processExtender.WaitForExit(); // Sometimes won't fire Exited, gotta call when-done manually, but no more than once
					Interlocked.Exchange(ref FFireResult, null)?.Invoke();
				}
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
