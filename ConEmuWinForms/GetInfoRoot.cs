using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml;

using ConEmu.WinForms.Util;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	/// Handles the <c>GetInfo</c> GuiMacro for the <c>Root</c> command.
	/// </summary>
	public class GetInfoRoot
	{
		private GetInfoRoot([NotNull] string name, uint? pid, bool isRunning, int? exitCode)
		{
			if(name == null)
				throw new ArgumentNullException(nameof(name));
			Name = name;
			Pid = pid;
			IsRunning = isRunning;
			ExitCode = exitCode;
		}

		/// <summary>
		/// Exit code of the payload process, if it has exited.
		/// </summary>
		public readonly int? ExitCode;

		/// <summary>
		/// Whether the payload process were running at the moment when the macro were executed.
		/// </summary>
		public readonly bool IsRunning;

		/// <summary>
		/// Name of the process, should always be available whether it's running or not (yet/already).
		/// </summary>
		[NotNull]
		public readonly string Name;

		/// <summary>
		/// The process ID, available only when the payload process is running.
		/// </summary>
		public readonly uint? Pid;

		[NotNull]
		public static async Task<GetInfoRoot> QueryAsync([NotNull] ConEmuSession session)
		{
			if(session == null)
				throw new ArgumentNullException(nameof(session));

			GuiMacroResult result = await session.BeginGuiMacro("GetInfo").WithParam("Root").ExecuteAsync();
			if(!result.IsSuccessful)
				throw new InvalidOperationException("The GetInfo-Root call did not succeed.");
			if(string.IsNullOrWhiteSpace(result.Response)) // Might yield an empty string randomly if not ready yet
				throw new InvalidOperationException("The GetInfo-Root call has yielded an empty result.");

			// Interpret the string as XML
			var xmlDoc = new XmlDocument();
			try
			{
				xmlDoc.LoadXml(result.Response);
			}
			catch(Exception ex)
			{
				// Could not parse the XML response. Not expected. Wait more.
				throw new InvalidOperationException($"The GetInfo-Root call result “{result.Response}” were not a valid XML document.", ex);
			}
			XmlElement xmlRoot = xmlDoc.DocumentElement;
			if(xmlRoot == null)
				throw new InvalidOperationException($"The GetInfo-Root call result “{result.Response}” didn't have a root XML element.");

			// Current possible records:
			// <Root Name="cmd.exe" />
			// <Root Name="cmd.exe" Running="true" PID="22088" ExitCode="259" UpTime="688343" />
			// <Root Name="cmd.exe" Running="false" PID="22088" ExitCode="0" UpTime="688343" />

			string sName = xmlRoot.GetAttribute("Name");

			bool isRunningRaw;
			bool? isRunning = bool.TryParse(xmlRoot.GetAttribute("Running"), out isRunningRaw) ? isRunningRaw : default(bool?); // Might mean the process hasn't started yet, in which case we get an empty attr and can't parse it

			uint nPidRaw;
			uint? nPid = ((isRunning == true) && (uint.TryParse(xmlRoot.GetAttribute("PID"), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out nPidRaw))) ? nPidRaw : default(uint?);

			int nExitCodeRaw;
			int? nExitCode = ((isRunning == false) && (int.TryParse(xmlRoot.GetAttribute("ExitCode"), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out nExitCodeRaw))) ? nExitCodeRaw : default(int?);

			return new GetInfoRoot(sName, nPid, isRunning ?? false, nExitCode);
		}
	}
}