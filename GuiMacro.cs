using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

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


        public string Execute(string asWhere, string asMacro)
        {
            if (ConEmuCD == IntPtr.Zero)
            {
                throw new GuiMacroException("ConEmuCD was not loaded");
            }

            string cmdLine = " -GuiMacro";
            if (!String.IsNullOrEmpty(asWhere))
                cmdLine += ":" + asWhere;
            cmdLine += " " + asMacro;

            // TODO: ConsoleMain3 uses pipes to output result
            // TODO: It may be better to improve ConEmuCD than implement pipes here?
            long iRc = ConsoleMain3.Invoke(0, cmdLine);
            switch (iRc)
            {
                case 200: case 201:
                    throw new GuiMacroException("Bad command line was passed to ConEmuCD");
            }

            return iRc.ToString();
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
