using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using ConEmu.WinForms;

namespace ControlDllTestbed
{
	internal static class Testbed
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static int Main()
		{
			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(RenderView(new Form()));
				return 0;
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return -1;
			}
		}

		private static Form RenderView(Form form)
		{
			form.Size = new Size(800, 600);

			ConEmuControl conemu;
			form.Controls.Add(conemu = new ConEmuControl() {Dock = DockStyle.Fill, MinimumSize = new Size(200, 200), IsStatusbarVisible = true});
			if(conemu.AutoStartInfo != null)
			{
				conemu.AutoStartInfo.SetEnv("one", "two");
				conemu.AutoStartInfo.SetEnv("geet", "huub");
				conemu.AutoStartInfo.GreetingText = "• Running \"cmd.exe\" as the default shell in the terminal. \n\n";
				//conemu.AutoStartInfo.GreetingText = "\"C:\\Program Files\\Git\\bin\\git.exe\" fetch --progress \"--all\" ";	// A test specimen with advanced quoting
				conemu.AutoStartInfo.IsEchoingConsoleCommandLine = true;
			}
			//conemu.AutoStartInfo = null;
			TextBox txt;
			form.Controls.Add(txt = new TextBox() {Text = "AnotherFocusableControl", AutoSize = true, Dock = DockStyle.Top});

			FlowLayoutPanel stack;
			form.Controls.Add(stack = new FlowLayoutPanel() {FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink});

			Button btn;
			stack.Controls.Add(btn = new Button() {Text = "Paste Command", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.RunningSession?.WriteInputText("whois microsoft.com" + Environment.NewLine); };

			stack.Controls.Add(btn = new Button() {Text = "Write StdOut", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.RunningSession?.WriteOutputText("\x001B7\x001B[90mEcho \"Hello world!\"\x001B[m\x001B8"); };

			stack.Controls.Add(btn = new Button() {Text = "Query HWND", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.RunningSession?.BeginGuiMacro("GetInfo").WithParam("HWND").ExecuteAsync().ContinueWith(task => txt.Text = $"ConEmu HWND: {Regex.Replace(task.Result.Response, "\\s+", " ")}", TaskScheduler.FromCurrentSynchronizationContext()); };

			stack.Controls.Add(btn = new Button() {Text = "Query PID", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.RunningSession?.BeginGuiMacro("GetInfo").WithParam("PID").ExecuteAsync().ContinueWith(task => txt.Text = $"ConEmu PID: {Regex.Replace(task.Result.Response, "\\s+", " ")}", TaskScheduler.FromCurrentSynchronizationContext()); };

			stack.Controls.Add(btn = new Button() {Text = "Kill Payload", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.RunningSession?.KillConsoleProcessAsync(); };

			stack.Controls.Add(btn = new Button() {Text = "Ctrl+C", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate { conemu.RunningSession?.SendControlCAsync(); };

			CheckBox checkStatusBar;
			stack.Controls.Add(checkStatusBar = new CheckBox() {Text = "StatusBar", Checked = conemu.IsStatusbarVisible});
			checkStatusBar.CheckedChanged += delegate { conemu.IsStatusbarVisible = checkStatusBar.Checked; };

			TextBox txtOutput = null;

			stack.Controls.Add(btn = new Button() {Text = "&Ping", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate
			{
				if(conemu.IsConsoleEmulatorOpen)
				{
					MessageBox.Show(form, "The console is busy right now.", "Ping", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return;
				}
				if(txtOutput == null)
					form.Controls.Add(txtOutput = new TextBox() {Multiline = true, Dock = DockStyle.Right, Width = 200});
				conemu.Start(new ConEmuStartInfo() {ConsoleProcessCommandLine = "ping ya.ru", IsEchoingConsoleCommandLine = true, AnsiStreamChunkReceivedEventSink = (sender, args) => txtOutput.Text += args.GetMbcsText(), WhenConsoleProcessExits = WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage, ConsoleProcessExitedEventSink = (sender, args) => txtOutput.Text += $"Exited with ERRORLEVEL {args.ExitCode}.", GreetingText = $"This will showcase getting the command output live in the backend.{Environment.NewLine}As the PING command runs, the textbox would duplicate its stdout in real time.{Environment.NewLine}{Environment.NewLine}"});
			};

			stack.Controls.Add(btn = new Button() {Text = "&Choice", AutoSize = true, Dock = DockStyle.Left});
			btn.Click += delegate
			{
				conemu.RunningSession?.CloseConsoleEmulator();
				DialogResult result = MessageBox.Show(form, "Keep terminal when payload exits?", "Choice", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
				if(result == DialogResult.Cancel)
					return;
				ConEmuSession session = conemu.Start(new ConEmuStartInfo() {ConsoleProcessCommandLine = "choice", IsEchoingConsoleCommandLine = true, WhenConsoleProcessExits = result == DialogResult.Yes ? WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage : WhenConsoleProcessExits.CloseConsoleEmulator, ConsoleProcessExitedEventSink = (sender, args) => MessageBox.Show($"Your choice is {args.ExitCode} (powered by startinfo event sink).")});
				session.WaitForConsoleProcessExitAsync().ContinueWith(task => MessageBox.Show($"Your choice is {task.Result.ExitCode} (powered by wait-for-exit-async)."));
			};

			return form;
		}
	}
}