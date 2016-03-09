namespace ConEmu.WinForms
{
	/// <summary>
	/// <see cref="ConEmuControl" /> states.
	/// </summary>
	public enum States
	{
		/// <summary>
		///     <para>There has been no console emulator opened in this control yet.</para>
		///     <para>The control is now empty, and <see cref="ConEmuControl.RunningSession" /> is <c>NULL</c>. A new session can be started with <see cref="ConEmuControl.Start" />.</para>
		/// </summary>
		Unused = 0,

		/// <summary>
		///     <para>The console emulator is open, and the console process is running in it.</para>
		///     <para><see cref="ConEmuControl.RunningSession" /> is available. A new session cannot be started until the current session is closed (<see cref="ConEmuSession.CloseConsoleEmulator" />).</para>
		/// </summary>
		ConsoleEmulatorWithConsoleProcess = 1,

		/// <summary>
		///     <para>The console emulator is still open, but the console process in it has already exited, even though the console view is still visible in the control.</para>
		///     <para>The console emulator stays open in this case if <see cref="ConEmuStartInfo.WhenConsoleProcessExits" /> allows it.</para>
		///     <para><see cref="ConEmuControl.RunningSession" /> is available. A new session cannot be started until the current session is closed (<see cref="ConEmuSession.CloseConsoleEmulator" />).</para>
		/// </summary>
		ConsoleEmulatorEmpty = 2,

		/// <summary>
		///     <para>There were a console emulator in this control, but its console process has exited, and then the terminal were closed.</para>
		///     <para>The control is now empty and not showing the console view, and <see cref="ConEmuControl.RunningSession" /> is <c>NULL</c>. A new session can be started with <see cref="ConEmuControl.Start" />.</para>
		/// </summary>
		Recycled = 3
	}
}