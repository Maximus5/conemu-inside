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
        public enum GuiMacroResult
        {
            // Succeeded
            gmrOk = 0,
            // Reserved for .Net control module
            gmrPending = 1,
            gmrDllNotLoaded = 2,
            gmrException = 3,
            // Bad PID or ConEmu HWND was specified
            gmrInvalidInstance = 4,
            // Unknown macro execution error in ConEmu
            gmrExecError = 5,
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int FConsoleMain3(int anWorkMode, string asCommandLine);

        public delegate void ExecuteResult(GuiMacroResult code, string data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int FGuiMacro(string asWhere, string asMacro, out IntPtr bstrResult);

        private string libraryPath;
        private IntPtr ConEmuCD;
        private FConsoleMain3 fnConsoleMain3;
        private FGuiMacro fnGuiMacro;

        public string LibraryPath
        {
            get
            {
                return libraryPath;
            }
        }


        protected string ExecuteLegacy(string asWhere, string asMacro)
        {
            if (ConEmuCD == IntPtr.Zero)
            {
                throw new GuiMacroException("ConEmuCD was not loaded");
            }
            if (fnConsoleMain3 == null)
            {
                throw new GuiMacroException("ConsoleMain3 function was not found");
            }


            string cmdLine = " -GuiMacro";
            if (!String.IsNullOrEmpty(asWhere))
                cmdLine += ":" + asWhere;
            cmdLine += " " + asMacro;

            Environment.SetEnvironmentVariable("ConEmuMacroResult", null);

            string result;

            int iRc = fnConsoleMain3.Invoke(3, cmdLine);

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

        protected void ExecuteHelper(string asWhere, string asMacro, ExecuteResult aCallbackResult)
        {
            if (aCallbackResult == null)
            {
                throw new GuiMacroException("aCallbackResult was not specified");
            }

            string result;
            GuiMacroResult code;

            // New ConEmu builds exports "GuiMacro" function
            if (fnGuiMacro != null)
            {
                IntPtr fnCallback = Marshal.GetFunctionPointerForDelegate(aCallbackResult);
                if (fnCallback == IntPtr.Zero)
                {
                    throw new GuiMacroException("GetFunctionPointerForDelegate failed");
                }

                IntPtr bstrPtr = IntPtr.Zero;
                int iRc = fnGuiMacro.Invoke(asWhere, asMacro, out bstrPtr);

                switch (iRc)
                {
                    case 0: // This is not expected for `GuiMacro` exported funciton, but JIC
                    case 133: // CERR_GUIMACRO_SUCCEEDED: expected
                        code = GuiMacroResult.gmrOk;
                        break;
                    case 134: // CERR_GUIMACRO_FAILED
                        code = GuiMacroResult.gmrExecError;
                        break;
                    default:
                        throw new GuiMacroException(string.Format("Internal ConEmuCD error: {0}", iRc));
                }

                if (bstrPtr == IntPtr.Zero)
                {
                	// Not expected, `GuiMacro` must return some BSTR in any case
                	throw new GuiMacroException("Empty bstrPtr was returned");
                }

                result = Marshal.PtrToStringBSTR(bstrPtr);
                Marshal.FreeBSTR(bstrPtr);

                if (result == null)
                {
                	// Not expected, `GuiMacro` must return some BSTR in any case
                	throw new GuiMacroException("Marshal.PtrToStringBSTR failed");
                }
            }
            else
            {
                result = ExecuteLegacy(asWhere, asMacro);
                code = GuiMacroResult.gmrOk;
            }

            aCallbackResult(code, result);
        }

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
                    ExecuteHelper(asWhere, asMacro, aCallbackResult);
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
            fnConsoleMain3 = null;
            fnGuiMacro = null;
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
            const string fnNameOld = "ConsoleMain3";
            IntPtr ptrConsoleMain = GetProcAddress(ConEmuCD, fnNameOld);
            const string fnNameNew = "GuiMacro";
            IntPtr ptrGuiMacro = GetProcAddress(ConEmuCD, fnNameNew);

            if ((ptrConsoleMain == IntPtr.Zero) && (ptrGuiMacro == IntPtr.Zero))
            {
                UnloadConEmuDll();
                throw new GuiMacroException(string.Format("Function {0} not found in library\n{1}\nUpdate ConEmu modules", fnNameOld, asLibrary));
            }

            if (ptrGuiMacro != IntPtr.Zero)
            {
                // To call: ExecGuiMacro.Invoke(asWhere, asCommand, callbackDelegate);
                fnGuiMacro = (FGuiMacro)Marshal.GetDelegateForFunctionPointer(ptrGuiMacro, typeof(FGuiMacro));
            }
            if (ptrConsoleMain != IntPtr.Zero)
            {
                // To call: ConsoleMain3.Invoke(0, cmdline);
                fnConsoleMain3 = (FConsoleMain3)Marshal.GetDelegateForFunctionPointer(ptrConsoleMain, typeof(FConsoleMain3));
            }
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
