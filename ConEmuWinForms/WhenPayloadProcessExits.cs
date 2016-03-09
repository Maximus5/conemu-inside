namespace ConEmu.WinForms
{
	/// <summary>
	/// Controls behavior of the console emulator when its payload process exits.
	/// </summary>
	public enum WhenPayloadProcessExits
	{
		/// <summary>
		/// When the payload command exits, the terminal window is hidden.
		/// The control shows blank space, output text disappears.
		/// <see cref="ConEmuSession.ConsoleProcessExited" /> and <see cref="ConEmuSession.ConsoleEmulatorClosed" /> are both fired at this moment.
		/// The terminal control is ready for running a new session.
		/// </summary>
		CloseTerminal,

		/// <summary>
		/// When the payload command exits, the terminal window stays visible, with all the console output text.
		/// No additional message is written to the console output.
		/// Pressing ESC or ENTER closes the terminal window, makes the terminal control blank, and allows to run further terminal sessions in this control.
		/// <see cref="ConEmuSession.ConsoleProcessExited" /> fires immediately, and <see cref="ConEmuSession.ConsoleEmulatorClosed" /> fires on user's ESC/ENTER.
		/// </summary>
		KeepTerminal,

		/// <summary>
		/// When the payload command exits, the terminal window stays visible, with all the console output text.
		/// The message “Press Enter or Esc to close console...” is displayed.
		/// Pressing ESC or ENTER closes the terminal window, makes the terminal control blank, and allows to run further terminal sessions in this control.
		/// <see cref="ConEmuSession.ConsoleProcessExited" /> fires immediately, and <see cref="ConEmuSession.ConsoleEmulatorClosed" /> fires on user's ESC/ENTER.
		/// </summary>
		KeepTerminalAndShowMessage
	}
}