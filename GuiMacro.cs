using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConEmuInside
{
    public class GuiMacroException : Exception
    {
        public GuiMacroException(string asMessage)
            : base(asMessage)
        {
        }
    }

    public class GuiMacro
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


        protected string Execute(string asWhere, string asMacro)
        {
            if (ConEmuCD == IntPtr.Zero)
            {
                throw new GuiMacroException("ConEmuCD was not loaded");
            }


            string cmdLine = " -GuiMacro";
            if (!String.IsNullOrEmpty(asWhere))
                cmdLine += ":" + asWhere;
            cmdLine += " " + asMacro;

            Environment.SetEnvironmentVariable("ConEmuMacroResult", null);

            string result;
            int iRc = ConsoleMain3.Invoke(3, cmdLine);
            switch (iRc)
            {
                case 200: // CERR_CMDLINEEMPTY
                case 201: // CERR_CMDLINE
                    throw new GuiMacroException("Bad command line was passed to ConEmuCD");
                case 0: // This is expected
                case 133: // CERR_GUIMACRO_SUCCEEDED: not expected, but...
                    result = Environment.GetEnvironmentVariable("ConEmuMacroResult");
                    if (result == null)
                        throw new GuiMacroException("ConEmuMacroResult was not set");
                    break;
                case 134: // CERR_GUIMACRO_FAILED
                    throw new GuiMacroException("GuiMacro execution failed");
                default:
                    throw new GuiMacroException(string.Format("Internal ConEmuCD error: {0}", iRc));
            }

            return result;
        }

        public enum GuiMacroResult
        {
            gmrOk = 0,
            gmrPending = 1,
            gmrDllNotLoaded = 2,
            gmrException = 3,
        };

        public delegate void ExecuteResult(GuiMacroResult code, string data);

        public GuiMacroResult Execute(string asWhere, string asMacro, ExecuteResult aCallbackResult)
        {
            if (ConEmuCD == IntPtr.Zero)
                return GuiMacroResult.gmrDllNotLoaded;

            new Thread(() =>
            {
                // Don't block application termination
                Thread.CurrentThread.IsBackground = true;
                // Start GuiMacro execution
                try
                {
                    string result = Execute(asWhere, asMacro);
                    aCallbackResult(GuiMacroResult.gmrOk, result);
                }
                catch (GuiMacroException e)
                {
                    aCallbackResult(GuiMacroResult.gmrException, e.Message);
                }
            }).Start();

            return GuiMacroResult.gmrPending;
        }

        public GuiMacro(string asLibrary)
        {
            ConEmuCD = IntPtr.Zero;
            libraryPath = asLibrary;
            LoadConEmuDll(asLibrary);
        }

        ~GuiMacro()
        {
            UnloadConEmuDll();
        }

        private void LoadConEmuDll(string asLibrary)
        {
            if (ConEmuCD != IntPtr.Zero)
            {
                return;
            }

            ConEmuCD = LoadLibrary(asLibrary);
            if (ConEmuCD == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new GuiMacroException(string.Format("Can't load library, ErrCode={0}\n{1}", errorCode, asLibrary));
            }

            // int __stdcall ConsoleMain3(int anWorkMode/*0-Server&ComSpec,1-AltServer,2-Reserved*/, LPCWSTR asCmdLine)
            const string fnName = "ConsoleMain3";
            IntPtr exportPtr = GetProcAddress(ConEmuCD, fnName);
            if (exportPtr == IntPtr.Zero)
            {
                UnloadConEmuDll();
                throw new GuiMacroException(string.Format("Function {0} not found in library\n{1}\nUpdate ConEmu modules", fnName, asLibrary));
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
    }

}
