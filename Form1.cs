using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace ConEmuInside
{
    public partial class ChildTerminal : Form
    {
        protected Process ConEmu;

        public ChildTerminal()
        {
            InitializeComponent();
        }

        private void ChildTerminal_Load(object sender, EventArgs e)
        {
            String lsOurDir, lsXmlFile, lsShell;
            lsOurDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            lsXmlFile = Path.Combine(lsOurDir, "ConEmu.xml");
            lsShell = @"{cmd}"; // Use ConEmu's default {cmd} task
            ConEmu = Process.Start("ConEmu.exe",
                " -InsideWnd 0x" + termPanel.Handle.ToString("X") +
                " -LoadCfgFile \"" + lsXmlFile + "\"" +
                " -cmd " + // This one MUST be the last switch
                lsShell // And the shell command line itself
                );
            // Start monitoring
            timer1.Start();
        }

        private string GetConEmuC()
        {
            // Returns Path to ConEmuC (to GuiMacro execution)

            if ((ConEmu == null) || ConEmu.HasExited)
                return null;
            if (ConEmu.Modules.Count == 0)
                return null;

            String lsExeDir, ConEmuC;
            lsExeDir = Path.GetDirectoryName(ConEmu.Modules[0].FileName);
            ConEmuC = Path.Combine(lsExeDir, @"ConEmu\ConEmuC.exe");
            if (!File.Exists(ConEmuC))
            {
                ConEmuC = Path.Combine(lsExeDir, @"ConEmuC.exe");
                if (!File.Exists(ConEmuC))
                {
                    ConEmuC = "ConEmuC.exe"; // Must not get here actually
                }
            }
            return ConEmuC;
        }

        private void ExecuteGuiMacro(string asMacro)
        {
            // conemuc.exe -silent -guimacro:1234 print("\e","git"," --version","\n")
            string ConEmuC = GetConEmuC();
            if (ConEmuC != null)
            {
                ProcessStartInfo macro = new ProcessStartInfo(
                   ConEmuC,
                   " -GuiMacro:" + ConEmu.Id.ToString() +
                     " " +
                     asMacro
                    );
                macro.WindowStyle = ProcessWindowStyle.Hidden;
                macro.CreateNoWindow = true;
                Process.Start(macro);
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
            if ((ConEmu != null) && ConEmu.HasExited)
            {
                timer1.Stop();
                ConEmu = null;
                RefreshControls(false);
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
    }
}
