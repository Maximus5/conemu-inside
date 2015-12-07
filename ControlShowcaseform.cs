using System.Drawing;
using System.Windows.Forms;

using ConEmu.WinForms;

namespace ConEmuInside
{
	public class ControlShowcaseForm : Form
	{
		public ControlShowcaseForm()
		{
			Controls.Add(new ConEmuControl() {Dock = DockStyle.Fill, MinimumSize = new Size(200,200), IsStartingImmediately = true});
			Controls.Add(new TextBox() {Text = "AnotherFocusableControl", AutoSize = true, Dock = DockStyle.Top});
		}
	}
}