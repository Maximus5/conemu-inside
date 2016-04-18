namespace ConEmu.WinForms
{
	/// <summary>
	/// Controls behavior of the console emulator when the console process running in it terminates.
	/// </summary>
	public enum WhenConsoleProcessExits
	{
		/// <summary>
		///     <para>When the console process exits, the console emulator is closed, and its window is hidden from the control.</para>
		///     <para>The control shows blank space, output text disappears.</para>
		///     <para><see cref="ConEmuSession.ConsoleProcessExited" /> and <see cref="ConEmuSession.ConsoleEmulatorClosed" /> are both fired at this moment.</para>
		///     <para>The <see cref="ConEmuControl">console emulator control</see> is ready for running a new <see cref="ConEmuSession">session</see>.</para>
		/// </summary>
		CloseConsoleEmulator,

		/// <summary>
		///     <para>When the console process exits, the console emulator stays open, and its window remains visible in the control, with all the console output text.</para>
		///     <para>No additional message is written to the console output, but you can still use <see cref="ConEmuSession.WriteOutputText" /> to write any text.</para>
		///     <para>Pressing ESC or ENTER closes the console emulator, makes the control blank, and allows to run further <see cref="ConEmuSession">console emulator sessions</see> in this control.</para>
		///     <para><see cref="ConEmuSession.ConsoleProcessExited" /> fires immediately, and <see cref="ConEmuSession.ConsoleEmulatorClosed" /> fires on user's ESC/ENTER, or when the console emulator is closed programmatically.</para>
		/// </summary>
		KeepConsoleEmulator,

		/// <summary>
		///     <para>When the console process exits, the console emulator stays open, and its window remains visible in the control, with all the console output text.</para>
		///     <para>The message <c>“Press Enter or Esc to close console...”</c> is displayed.</para>
		///     <para>Pressing ESC or ENTER closes the console emulator, makes the control blank, and allows to run further <see cref="ConEmuSession">console emulator sessions</see> in this control.</para>
		///     <para><see cref="ConEmuSession.ConsoleProcessExited" /> fires immediately, and <see cref="ConEmuSession.ConsoleEmulatorClosed" /> fires on user's ESC/ENTER, or when the console emulator is closed programmatically.</para>
		/// </summary>
		KeepConsoleEmulatorAndShowMessage
	}
}