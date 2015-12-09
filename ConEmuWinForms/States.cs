namespace ConEmu.WinForms
{
	public enum States
	{
		/// <summary>
		/// There has been no console process executed in this control yet.
		/// </summary>
		Empty,

		/// <summary>
		/// The console process is currently running.
		/// </summary>
		Running,

		/// <summary>
		/// The console process has been run in this control, but has exited.
		/// </summary>
		Exited
	}
}