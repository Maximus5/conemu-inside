using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
		private GetInfoRoot(States state, [NotNull] string name, uint? pid, int? exitCode)
		{
			if(name == null)
				throw new ArgumentNullException(nameof(name));
			Name = name;
			Pid = pid;
			State = state;
			ExitCode = exitCode;
		}

		/// <summary>
		/// Exit code of the payload process, if it has exited.
		/// </summary>
		public readonly int? ExitCode;

		/// <summary>
		/// Whether the payload process were running at the moment when the macro were executed.
		/// </summary>
		public bool IsRunning => State == States.Running;

		/// <summary>
		/// Name of the process, should always be available whether it's running or not (yet/already).
		/// </summary>
		[NotNull]
		public readonly string Name;

		/// <summary>
		/// The process ID, available only when the payload process is running.
		/// </summary>
		public readonly uint? Pid;

		/// <summary>
		/// The current state of the root console process, as in the <c>GetInfo Root</c> GUI Macro <c>State</c> field: Empty, NotStarted, Running, Exited.
		/// </summary>
		public readonly States State;

		[NotNull]
		public static async Task<GetInfoRoot> QueryAsync([NotNull] ConEmuSession session)
		{
			if(session == null)
				throw new ArgumentNullException(nameof(session));

			GuiMacroResult result = await session.BeginGuiMacro("GetInfo").WithParam("Root").ExecuteAsync();
			Trace.WriteLine("[ROOT]: " + result.Response);
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
			// <Root State="NotStarted" Name="cmd.exe" />
			// <Root State="Running" Name="cmd.exe" PID="4672" ExitCode="259" UpTime="4406" />
			// <Root State="Exited" Name="cmd.exe" PID="4672" ExitCode="0" UpTime="14364"/>
			// Also, there's the State="Empty" documented, though it would be hard to catch

			// Start with detecting the state
			string sState = xmlRoot.GetAttribute("State");
			if(string.IsNullOrWhiteSpace(sState))
				throw new InvalidOperationException($"The GetInfo-Root call result “{result.Response}” didn't specify the current ConEmu state.");
			States state;
			if(!Enum.TryParse(sState, false, out state))
				throw new InvalidOperationException($"The GetInfo-Root call result “{result.Response}” specifies the State “{sState}” which cannot be matched against the list of the known states {Enum.GetValues(typeof(States)).OfType<States>().Select(o => o.ToString()).OrderBy(o => o, StringComparer.InvariantCultureIgnoreCase).Aggregate((x, y) => x + ", " + y)}.");

			string sName = xmlRoot.GetAttribute("Name");

			uint nPidRaw;
			uint? nPid = ((state == States.Running) && (uint.TryParse(xmlRoot.GetAttribute("PID"), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out nPidRaw))) ? nPidRaw : default(uint?);

			int nExitCodeRaw;
			int? nExitCode = ((state == States.Exited) && (int.TryParse(xmlRoot.GetAttribute("ExitCode"), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out nExitCodeRaw))) ? nExitCodeRaw : default(int?);

			return new GetInfoRoot(state, sName, nPid, nExitCode);
		}

		/// <summary>
		/// State: Empty, NotStarted, Running, Exited.
		/// </summary>
		public enum States
		{
			/// <summary>
			/// If there are not consoles in ConEmu.
			/// </summary>
			/// <example><code>&lt;Root State="Empty" /&gt;</code></example>
			Empty,

			/// <summary>
			/// If console initialization is in progress (<c>ping localhost -t</c> for example).
			/// </summary>
			/// <example><code>&lt;Root State="NotStarted" Name="ping.exe" /&gt;</code></example>
			NotStarted,

			/// <summary>
			/// If root process was started and is running. Note, <c>259</c> in <c>ExitCode</c> is <c>STILL_ACTIVE</c> constant.
			/// </summary>
			/// <example><code>&lt;Root State="Running" Name="ping.exe" PID="7136" ExitCode="259" UpTime="3183" /&gt;</code></example>
			Running,

			/// <summary>
			///     <para>• If root process was finished (terminated by `Ctrl+C` as example).</para>
			///     <para>• Another example for `cmd.exe` normal exit.</para>
			/// </summary>
			/// <example><code>&lt;Root State="Exited" Name="ping.exe" PID="7136" ExitCode="3221225786" UpTime="10195" /&gt;</code></example>
			/// <example><code>&lt;Root State="Exited" Name="cmd.exe" PID="6688" ExitCode="0" UpTime="1825" /&gt;</code></example>
			Exited
		}
	}
}