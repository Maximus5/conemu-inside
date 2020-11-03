using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace ConEmuInside
{
    public partial class ChildTerminal : Form
    {
        private Process conemu;
        private GuiMacro guiMacro;
        private readonly TerminalStarter starter;
        private ConEmuStartArgs args;

        internal ChildTerminal(TerminalStarter starter, ConEmuStartArgs args)
        {
            this.starter = starter;
            this.args = args;
            InitializeComponent();
        }

        private void ChildTerminal_Load(object sender, EventArgs e)
        {
            this.Left = this.starter.Left + 100;
            this.Top = this.starter.Top + 100;
            RefreshControls(false);
            termPanel.Resize += new System.EventHandler(this.termPanel_Resize);
            StartConEmu();
        }

        private void RefreshControls(bool bTermActive)
        {
            if (bTermActive)
            {
                AcceptButton = null;
                groupBox2.Enabled = true;
                if (!termPanel.Visible)
                {
                    termPanel.Visible = true;
                }
                if (!termPanel.Enabled)
                {
                    termPanel.Enabled = true;
                }
            }
            else
            {
                if (termPanel.Enabled)
                {
                    termPanel.Enabled = false;
                }
                groupBox2.Enabled = false;
            }
            starter.RefreshControls(bTermActive);
        }

        internal string GetConEmuExe()
        {
            bool bExeLoaded = false;
            string lsConEmuExe = null;

            while (!bExeLoaded && (conemu != null) && !conemu.HasExited)
            {
                try
                {
                    lsConEmuExe = conemu.Modules[0].FileName;
                    bExeLoaded = true;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    Thread.Sleep(50);
                }
            }

            return lsConEmuExe;
        }

        // Returns Path to "ConEmuCD[64].dll" (to GuiMacro execution)
        internal string GetConEmuDll()
        {
            // Query real (full) path of started executable
            string lsConEmuExe = GetConEmuExe();
            if (lsConEmuExe == null)
                return null;

            // Determine bitness of **our** process
            string lsDll = (IntPtr.Size == 8) ? "ConEmuCD64.dll" : "ConEmuCD.dll";

            // Ready to find the library
            String lsExeDir, ConEmuCD;
            lsExeDir = Path.GetDirectoryName(lsConEmuExe);
            ConEmuCD = Path.Combine(lsExeDir, "ConEmu\\" + lsDll);
            if (!File.Exists(ConEmuCD))
            {
                ConEmuCD = Path.Combine(lsExeDir, lsDll);
                if (!File.Exists(ConEmuCD))
                {
                    ConEmuCD = lsDll; // Must not get here actually
                }
            }

            return ConEmuCD;
        }

        private void ExecuteGuiMacro(string asMacro)
        {
            // conemuc.exe -silent -guimacro:1234 print("\e","git"," --version","\n")
            string conemuDll = GetConEmuDll();
            if (conemuDll == null)
            {
                throw new GuiMacroException("ConEmuCD must not be null");
            }

            if (guiMacro != null && guiMacro.LibraryPath != conemuDll)
            {
                guiMacro = null;
            }

            try
            {
                if (guiMacro == null)
                    guiMacro = new GuiMacro(conemuDll);
                guiMacro.Execute(conemu.Id.ToString(), asMacro,
                    (GuiMacro.GuiMacroResult code, string data) => {
                        Debugger.Log(0, "GuiMacroResult", "code=" + code.ToString() + "; data=" + data + "\n");
                    });
            }
            catch (GuiMacroException e)
            {
                MessageBox.Show(e.Message, "GuiMacroException", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void printBtn_Click(object sender, EventArgs e)
        {
            if (promptBox.Text == "")
                return;
            String lsMacro;
            lsMacro = "Print(@\"" + promptBox.Text.Replace("\"", "\"\"") + "\",\"\n\")";
            ExecuteGuiMacro(lsMacro);
            promptBox.SelectAll();
        }

        private void macroBtn_Click(object sender, EventArgs e)
        {
            if (promptBox.Text == "")
                return;
            ExecuteGuiMacro(promptBox.Text);
            promptBox.SelectAll();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if ((conemu != null) && conemu.HasExited)
            {
                timer1.Stop();
                conemu = null;
                RefreshControls(false);
                Close();
            }
        }

        private void promptBox_KeyDown(object sender, KeyEventArgs e)
        {
            //TODO: Enter and Leave events are not triggered when focus is put into ConEmu window
            if (e.KeyValue == 13)
            {
                printBtn_Click(sender, null);
            }
        }

        private void promptBox_Enter(object sender, EventArgs e)
        {
            //TODO: Enter and Leave events are not triggered when focus is put into ConEmu window
            //AcceptButton = printBtn;
            //promptBox.Text = "...in...";
        }

        private void promptBox_Leave(object sender, EventArgs e)
        {
            //TODO: Enter and Leave events are not triggered when focus is put into ConEmu window
            //if (AcceptButton == printBtn)
            //AcceptButton = null;
            //promptBox.Text = "...out...";
        }

        internal void StartConEmu()
        {
            RefreshControls(true);

            var sRunAs = this.args.runAs ? " -cur_console:a" : "";

            var sRunArgs = (this.args.debug ? " -debugw" : "") +
                           " -NoKeyHooks" +
                           " -InsideWnd 0x" + termPanel.Handle.ToString("X") +
                           " -LoadCfgFile \"" + this.args.xmlFilePath + "\"" +
                           " -Dir \"" + this.args.startupDirectory + "\"" +
                           (this.args.log ? " -Log" : "") +
                           (this.args.useGuiMacro ? " -detached" : " -run " + this.args.cmdLine + sRunAs);

            if (this.args.useGuiMacro)
            {
                promptBox.Text = "Shell(\"new_console\", \"\", \"" +
                                 (this.args.cmdLine + sRunAs).Replace("\"", "\\\"") +
                                 "\")";
            }

            try
            {
                // Start ConEmu
                conemu = Process.Start(this.args.conemuExePath, sRunArgs);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                RefreshControls(false);
                MessageBox.Show(ex.Message + "\r\n\r\n" +
                                "Command:\r\n" + this.args.conemuExePath + "\r\n\r\n" +
                                "Arguments:\r\n" + sRunArgs,
                    ex.GetType().FullName + " (" + ex.NativeErrorCode.ToString() + ")",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RefreshControls(true);
            // Start monitoring
            timer1.Start();

            // Execute "startup" macro
            if (this.args.useGuiMacro)
            {   
                macroBtn_Click(null, null);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr FindWindowEx(IntPtr hParent, IntPtr hChild, string szClass, string szWindow);

        private void termPanel_Resize(object sender, EventArgs e)
        {
            if (conemu != null)
            {
                IntPtr hConEmu = FindWindowEx(termPanel.Handle, (IntPtr)0, null, null);
                if (hConEmu != (IntPtr)0)
                {
                    //MoveWindow(hConEmu, 0, 0, termPanel.Width, termPanel.Height, true);
                }
            }
        }

        private void closeBtn_Click(object sender, EventArgs e)
        {
            ExecuteGuiMacro("Close(2,1)");
        }

        private void ChildTerminal_FormClosed(object sender, FormClosedEventArgs e)
        {
            starter.RefreshControls(false);
        }
    }
}
