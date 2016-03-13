using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using ConEmu.WinForms;

namespace ConsoleUtilityShowcase
{
	internal static class Program
	{
		private static Form CreateMainForm()
		{
			var form = new Form() {AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10), Text = "Console Utility in a Terminal"};

			FlowLayoutPanel stack;
			form.Controls.Add(stack = new FlowLayoutPanel() {Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown});

			stack.Controls.Add(new Label() {AutoSize = true, Dock = DockStyle.Top, Text = "This sample illustrates running a console utility\npresenting the user a real terminal window to its console\nas a control embedded in the form.\n\nThe program also gets the output of the utility,\nthough presenting its progress to the user is the main goal.\n\n"});

			Button btnPing;
			stack.Controls.Add(btnPing = new Button() {Text = "Run ping", Dock = DockStyle.Left});
			btnPing.Click += delegate { CreatePingForm().ShowDialog(form); };
			return form;
		}

		private static Form CreatePingForm()
		{
			var form = new Form() {AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10), Text = "Ping Command"};

			FlowLayoutPanel stack;
			form.Controls.Add(stack = new FlowLayoutPanel() {Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown});

			stack.Controls.Add(new Label() {AutoSize = true, Dock = DockStyle.Top, Text = "Running the ping command."});
			Label labelWaitOrResult;
			stack.Controls.Add(labelWaitOrResult = new Label() {AutoSize = true, Dock = DockStyle.Top, Text = "Please wait…"});

			ConEmuControl conemu;
			var sbText = new StringBuilder();
			stack.Controls.Add(conemu = new ConEmuControl() {AutoStartInfo = null, MinimumSize = new Size(800, 600), Dock = DockStyle.Top});
			ConEmuSession session = conemu.Start(new ConEmuStartInfo() {AnsiStreamChunkReceivedEventSink = (sender, args) => sbText.Append(args.GetMbcsText()), ConsoleProcessCommandLine = "ping 8.8.8.8"});
			session.ConsoleProcessExited += delegate
			{
				Match match = Regex.Match(sbText.ToString(), @"\(.*\b(?<pc>\d+)%\b.*?\)");
				if(!match.Success)
					labelWaitOrResult.Text = "Ping execution completed, failed to parse the result.";
				else
					labelWaitOrResult.Text = $"Ping execution completed, lost {match.Groups["pc"].Value} per cent of packets.";
			};
			session.ConsoleEmulatorClosed += delegate { form.Close(); };

			return form;
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(CreateMainForm());
		}
	}
}