using System;

namespace ConEmu.WinForms
{
	public class ProcessExitedEventArgs : EventArgs
	{
		private readonly int _exitcode;

		public ProcessExitedEventArgs(int exitcode)
		{
			_exitcode = exitcode;
		}

		/// <summary>
		/// Gets the exit code of the process.
		/// </summary>
		public int ExitCode
		{
			get
			{
				return _exitcode;
			}
		}
	}
}