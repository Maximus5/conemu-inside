namespace ConEmu.WinForms
{
	/// <summary>
	/// <see cref="ConEmuControl" /> states.
	/// </summary>
	public enum States
	{
		/// <summary>
		///     <para>There has been no terminal opened in this control yet.</para>
		///     <para>The control is now empty, and <see cref="ConEmuControl.RunningSession" /> is <c>NULL</c>.</para>
		/// </summary>
		Empty,

		/// <summary>
		///     <para>The terminal is open, and the payload console process is running in it.</para>
		///     <para><see cref="ConEmuControl.RunningSession" /> is available.</para>
		/// </summary>
		TerminalWithConsoleProcess,

		/// <summary>
		///     <para>The terminal is still open, but the payload console process in it has already exited.</para>
		///     <para>The terminal stays open in this case if <see cref="ConEmuStartInfo.WhenConsoleProcessExits" /> allows it.</para>
		///     <para><see cref="ConEmuControl.RunningSession" /> is available.</para>
		/// </summary>
		DetachedTerminal,

		/// <summary>
		///     <para>There were a terminal in this control, but its payload console process has exited, and the terminal has closed.</para>
		///     <para>The control is now empty, and <see cref="ConEmuControl.RunningSession" /> is <c>NULL</c>.</para>
		/// </summary>
		Exited
	}
}