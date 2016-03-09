using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	public static class ConEmuConstants
	{
		[NotNull]
		public static readonly string ConEmuConsoleExtenderExeName = "ConEmuC.exe";

		[NotNull]
		public static readonly string ConEmuConsoleServerFileNameNoExt = "ConEmuCD";

		[NotNull]
		public static readonly string ConEmuExeName = "conemu.exe";

		[NotNull]
		public static readonly string ConEmuSubfolderName = "ConEmu";

		/// <summary>
		/// The default for <see cref="ConEmuStartInfo.ConsoleProcessCommandLine" />.
		/// Runs the stock ConEmu task for the Windows command line.
		/// </summary>
		public static readonly string DefaultConsoleCommandLine = "{cmd}";
	}
}