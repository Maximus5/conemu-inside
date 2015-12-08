using System;
using System.Drawing;
using System.Windows.Forms;

using ConEmu.WinForms;

namespace ConEmuInside
{
	public class ControlShowcaseForm : Form
	{
		public ControlShowcaseForm()
		{
			ConEmuControl conemu;
			Controls.Add(conemu = new ConEmuControl() {Dock = DockStyle.Fill, MinimumSize = new Size(200,200), IsStartingImmediately = true, IsStatusbarVisible = true});
			conemu.SetEnv("one", "two");
			conemu.SetEnv("geet", "huub");
			Controls.Add(new TextBox() {Text = "AnotherFocusableControl", AutoSize = true, Dock = DockStyle.Top});
			Button btn;
			Controls.Add(btn = new Button() {Text = "Exec Command", AutoSize = true, Dock = DockStyle.Top});
			btn.Click += delegate {conemu.PasteText("whois microsoft.com" + Environment.NewLine);  };
		}
	}
}