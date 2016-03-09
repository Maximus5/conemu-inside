using System;

namespace ConEmu.WinForms
{
	/// <summary>
	/// Gives the exit code of the console process when it exits in the console emulator.
	/// </summary>
	public class ConsoleProcessExitedEventArgs : EventArgs
	{
		public ConsoleProcessExitedEventArgs(int exitcode)
		{
			ExitCode = exitcode;
		}

		/// <summary>
		/// Gets the exit code of the process.
		/// </summary>
		public int ExitCode { get; }
	}
}