namespace ConEmu.WinForms
{
	/// <summary>
	/// A result of executing the GUI macro.
	/// </summary>
	public struct GuiMacroResult
	{
		/// <summary>
		/// Whether macro execution returned success.
		/// </summary>
		public bool IsSuccessful;

		/// <summary>
		/// String response of the command, “<c>OK</c>” if successful and without output.
		/// </summary>
		public string Response;

		public override string ToString()
		{
			return $"{Response} ({(IsSuccessful ? "Succeeded" : "Failed")})";
		}
	}
}