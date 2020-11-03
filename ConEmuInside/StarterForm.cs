using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace ConEmuInside
{
    public partial class TerminalStarter : Form
    {
        protected ChildTerminal terminal;

        public TerminalStarter()
        {
            InitializeComponent();
        }

        private void TerminalStarter_Load(object sender, EventArgs e)
        {
            argConEmuExe.Text = GetConEmu();
            argDirectory.Text = Directory.GetCurrentDirectory();
            var lsOurDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            argXmlFile.Text = Path.Combine(lsOurDir, "ConEmu.xml");
            argCmdLine.Text = @"{cmd}"; // Use ConEmu's default {cmd} task
            RefreshControls(false);
            // Force focus to ‘cmd line’ control
            argCmdLine.Select();
        }

        internal void RefreshControls(bool bTermActive)
        {
            if (bTermActive)
            {
                AcceptButton = null;
                if (startPanel.Enabled)
                {
                    startPanel.Enabled = false;
                }
            }
            else
            {
                if (!startPanel.Enabled)
                {
                    startPanel.Enabled = true;
                    argCmdLine.Focus();
                }
                AcceptButton = startBtn;
            }


        }

        internal string GetConEmu()
        {
            string sOurDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            string[] sSearchIn = {
              Directory.GetCurrentDirectory(),
              sOurDir,
              Path.Combine(sOurDir, ".."),
              Path.Combine(sOurDir, "ConEmu"),
              "%PATH%", "%REG%"
              };

            string[] sNames;
            sNames = new string[] { "ConEmu.exe", "ConEmu64.exe" };

            foreach (string sd in sSearchIn)
            {
                foreach (string sn in sNames)
                {
                    string spath;
                    if (sd == "%PATH%" || sd == "%REG%")
                    {
                        spath = sn; //TODO
                    }
                    else
                    {
                        spath = Path.Combine(sd, sn);
                    }
                    if (File.Exists(spath))
                        return spath;
                }
            }

            // Default
            return "ConEmu.exe";
        }

        private void exeBtn_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "Choose ConEmu main executable";
            openFileDialog1.FileName = argConEmuExe.Text;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                argConEmuExe.Text = openFileDialog1.FileName;
            }
            argConEmuExe.Focus();
        }

        private void cmdBtn_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "Choose startup shell";
            openFileDialog1.FileName = "";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                argCmdLine.Text = openFileDialog1.FileName;
            }
            argCmdLine.Focus();
        }

        private void dirBtn_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.SelectedPath = argDirectory.Text;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                argDirectory.Text = folderBrowserDialog1.SelectedPath;
            }
            argDirectory.Focus();
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            var args = new ConEmuStartArgs
            {
                runAs = argRunAs.Checked,
                debug = argDebug.Checked,
                log = argLog.Checked,
                autoClose = argAutoClose.Checked,
                useGuiMacro = argUseGuiMacro.Checked,
                xmlFilePath = argXmlFile.Text,
                conemuExePath = argConEmuExe.Text,
                cmdLine = argCmdLine.Text,
                startupDirectory = argDirectory.Text,
            };

            terminal = new ChildTerminal(this, args);
            terminal.ShowDialog(this);
        }

        private void startArgs_Enter(object sender, EventArgs e)
        {
            AcceptButton = startBtn;
        }
    }
}
