namespace ConEmu.WinForms
{
	/// <summary>
	/// A result of executing the GUI macro.
	/// </summary>
	public struct GuiMacroResult
	{
		/// <summary>
		/// ERRORLEVEL of the ConEmu Console Extender process.
		/// </summary>
		public int ErrorLevel;

		/// <summary>
		/// String response of the command, “<c>OK</c>” if successful and without output.
		/// </summary>
		public string Response;
	}
}