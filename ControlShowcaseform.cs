using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using ConEmu.WinForms;

namespace ConEmuInside
{
	public class ControlShowcaseForm : Form
	{
		public ControlShowcaseForm()
		{
			ConEmuControl conemu;
			Controls.Add(conemu = new ConEmuControl() {Dock = DockStyle.Fill, MinimumSize = new Size(200, 200), IsStartingImmediately = true, IsStatusbarVisible = true});
			conemu.SetEnv("one", "two");
			conemu.SetEnv("geet", "huub");
			TextBox txt;
			Controls.Add(txt = new TextBox() {Text = "AnotherFocusableControl", AutoSize = true, Dock = DockStyle.Top});

			FlowLayoutPanel stack;
			Controls.Add(stack = new FlowLayoutPanel() {FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink});

			Button btn;
			stack.Controls.Add(btn = new Button() {Text = "Exec Command", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.PasteText("whois microsoft.com" + Environment.NewLine); };

			stack.Controls.Add(btn = new Button() {Text = "Query HWND", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.BeginGuiMacro("GetInfo").WithParam("HWND").Execute(result => txt.Text = $"ConEmu HWND: {Regex.Replace(result.Response, "\\s+", " ")}"); };

			stack.Controls.Add(btn = new Button() {Text = "Query PID", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.BeginGuiMacro("GetInfo").WithParam("PID").Execute(result => txt.Text = $"ConEmu PID: {Regex.Replace(result.Response, "\\s+", " ")}"); };

			CheckBox checkStatusBar;
			stack.Controls.Add(checkStatusBar = new CheckBox() {Text = "StatusBar", Checked = conemu.IsStatusbarVisible});
			checkStatusBar.CheckedChanged += delegate { conemu.IsStatusbarVisible = checkStatusBar.Checked; };
		}
	}
}