using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

using ConEmu.WinForms.Util;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.Threading;

using Timer = System.Windows.Forms.Timer;

namespace ConEmu.WinForms
{
	/// <summary>
	///     <para>A single session of the console emulator running a console process. Each console process execution in the control spawns a new console emulator and a new session.</para>
	///     <para>When the console emulator starts, a console view appears in the control. The console process starts running in it immediately. When the console process terminates, the console emulator might or might not be closed, depending on the settings. After the console emulator closes, the control stops viewing the console, and this session ends.</para>
	/// </summary>
	public class ConEmuSession
	{
		/// <summary>
		/// A service option. Whether to load the ConEmu helper DLL in-process to communicate with ConEmu (<c>True</c>, new mode), or start a new helper process to send each command (<c>False</c>, legacy mode).
		/// </summary>
		private static readonly bool IsExecutingGuiMacrosInProcess = true;

		/// <summary>
		/// Non-NULL if we've requested ANSI log from ConEmu and are listening to it.
		/// </summary>
		[CanBeNull]
		private readonly AnsiLog _ansilog;

		/// <summary>
		/// Per-session temp files, like the startup options for ConEmu and ANSI log cache.
		/// </summary>
		[NotNull]
		private readonly DirectoryInfo _dirTempWorkingFolder;

		/// <summary>
		/// Sends commands to the ConEmu instance and gets info from it.
		/// </summary>
		[NotNull]
		private readonly GuiMacroExecutor _guiMacroExecutor;

		/// <summary>
		/// Executed to process disposal.
		/// </summary>
		[NotNull]
		private readonly List<Action> _lifetime = new List<Action>();

		/// <summary>
		/// The exit code of the console process, if it has already exited. <c>Null</c>, if the console process is still running within the console emulator.
		/// </summary>
		private int? _nConsoleProcessExitCode;

		/// <summary>
		/// The ConEmu process, even after it exits.
		/// </summary>
		[NotNull]
		private readonly Process _process;

		/// <summary>
		/// Stores the joinable task factory used for executing work on the main thread, so that all state properties
		/// were only changed on this thread.
		/// </summary>
		[NotNull]
		private readonly JoinableTaskFactory _joinableTaskFactory;

		/// <summary>
		/// The original parameters for this session; sealed, so they can't change after the session is run.
		/// </summary>
		[NotNull]
		private readonly ConEmuStartInfo _startinfo;

		/// <summary>
		/// Task-based notification of the console emulator closing.
		/// </summary>
		[NotNull]
		private readonly TaskCompletionSource<Missing> _taskConsoleEmulatorClosed = new TaskCompletionSource<Missing>();

		/// <summary>
		/// Task-based notification of the console process exiting.
		/// </summary>
		[NotNull]
		private readonly TaskCompletionSource<ConsoleProcessExitedEventArgs> _taskConsoleProcessExit = new TaskCompletionSource<ConsoleProcessExitedEventArgs>();

		/// <summary>
		/// Starts the session.
		/// Opens the emulator view in the control (HWND given in <paramref name="hostcontext" />) by starting the ConEmu child process and giving it that HWND; ConEmu then starts the child Console Process for the commandline given in <paramref name="startinfo" /> and makes it run in the console emulator window.
		/// </summary>
		/// <param name="startinfo">User-defined startup parameters for the console process.</param>
		/// <param name="hostcontext">Control-related parameters.</param>
		/// <param name="joinableTaskFactory">The <see cref="JoinableTaskFactory"/>.</param>
		public ConEmuSession([NotNull] ConEmuStartInfo startinfo, [NotNull] HostContext hostcontext, [NotNull] JoinableTaskFactory joinableTaskFactory)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(hostcontext == null)
				throw new ArgumentNullException(nameof(hostcontext));
			if (joinableTaskFactory == null)
				throw new ArgumentNullException(nameof(joinableTaskFactory));
			if(string.IsNullOrEmpty(startinfo.ConsoleProcessCommandLine))
				throw new InvalidOperationException($"Cannot start a new console process for command line “{startinfo.ConsoleProcessCommandLine}” because it's either NULL, or empty, or whitespace.");

			_joinableTaskFactory = joinableTaskFactory;
			_startinfo = startinfo;
			startinfo.MarkAsUsedUp(); // No more changes allowed in this copy

			// Directory for working files, +cleanup
			_dirTempWorkingFolder = Init_TempWorkingFolder();

			// Events wiring: make sure sinks pre-installed with start-info also get notified
			Init_WireEvents(startinfo);

			// Should feed ANSI log?
			if(startinfo.IsReadingAnsiStream)
				_ansilog = Init_AnsiLog(startinfo);

			// Cmdline
			CommandLineBuilder cmdl = Init_MakeConEmuCommandLine(startinfo, hostcontext, _ansilog, _dirTempWorkingFolder);

			// Start ConEmu
			// If it fails, lifetime will be terminated; from them on, termination will be bound to ConEmu process exit
			_process = Init_StartConEmu(startinfo, cmdl);

			// GuiMacro executor
			_guiMacroExecutor = new GuiMacroExecutor(startinfo.ConEmuConsoleServerExecutablePath);
			_lifetime.Add(() => ((IDisposable)_guiMacroExecutor).Dispose());

			// Monitor payload process
			Init_ConsoleProcessMonitoring();
		}

		/// <summary>
		///     <para>Gets whether the console process has already exited (see <see cref="ConsoleProcessExited" />). The console emulator view might have closed as well, but might have not (see <see cref="ConEmuStartInfo.WhenConsoleProcessExits" />).</para>
		///     <para>This state only changes on the main thread.</para>
		/// </summary>
		public bool IsConsoleProcessExited => _nConsoleProcessExitCode.HasValue;

		/// <summary>
		///     <para>Gets the start info with which this session has been started.</para>
		///     <para>All of the properties in this object are now readonly.</para>
		/// </summary>
		[NotNull]
		public ConEmuStartInfo StartInfo => _startinfo;

		/// <summary>
		/// Starts construction of the ConEmu GUI Macro, see http://conemu.github.io/en/GuiMacro.html .
		/// </summary>
		[Pure]
		public GuiMacroBuilder BeginGuiMacro([NotNull] string sMacroName)
		{
			if(sMacroName == null)
				throw new ArgumentNullException(nameof(sMacroName));

			return new GuiMacroBuilder(this, sMacroName, Enumerable.Empty<string>());
		}

		/// <summary>
		/// An alias for <see cref="CloseConsoleEmulator" />.
		/// </summary>
		public void Close()
		{
			CloseConsoleEmulator();
		}

		/// <summary>
		///     <para>Closes the console emulator window, and kills the console process if it's still running.</para>
		///     <para>This also closes the running session, the control goes blank and ready for running a new session.</para>
		///     <para>To just kill the console process, use <see cref="KillConsoleProcessAsync" />. If <see cref="ConEmuStartInfo.WhenConsoleProcessExits" /> allows, the console emulator window might stay open after that.</para>
		/// </summary>
		public void CloseConsoleEmulator()
		{
			try
			{
				if(!_process.HasExited)
                    Thread.Sleep(10);
                if (!_process.HasExited)
					_process.Kill();
			}
			catch(Exception)
			{
				// Might be a race, so in between HasExited and Kill state could change, ignore possible errors here
			}
		}

		/// <summary>
		///     <para>Executes a ConEmu GUI Macro on the active console, see http://conemu.github.io/en/GuiMacro.html .</para>
		///     <para>This function takes for formatted text of a GUI Macro; to format parameters correctly, better use the <see cref="BeginGuiMacro" /> and the macro builder.</para>
		/// </summary>
		/// <param name="macrotext">The full macro command, see http://conemu.github.io/en/GuiMacro.html .</param>
		public Task<GuiMacroResult> ExecuteGuiMacroTextAsync([NotNull] string macrotext)
		{
			if(macrotext == null)
				throw new ArgumentNullException(nameof(macrotext));

			Process processConEmu = _process;
			if(processConEmu == null)
				throw new InvalidOperationException("Cannot execute a macro because the console process is not running at the moment.");

			return IsExecutingGuiMacrosInProcess ? _guiMacroExecutor.ExecuteInProcessAsync(processConEmu.Id, macrotext) : _guiMacroExecutor.ExecuteViaExtenderProcessAsync(macrotext, processConEmu.Id, _startinfo.ConEmuConsoleExtenderExecutablePath);
		}

		/// <summary>
		///     <para>Executes a ConEmu GUI Macro on the active console, see http://conemu.github.io/en/GuiMacro.html , synchronously.</para>
		///     <para>This function takes for formatted text of a GUI Macro; to format parameters correctly, better use the <see cref="BeginGuiMacro" /> and the macro builder.</para>
		/// </summary>
		/// <param name="macrotext">The full macro command, see http://conemu.github.io/en/GuiMacro.html .</param>
		public GuiMacroResult ExecuteGuiMacroTextSync([NotNull] string macrotext)
		{
			if(macrotext == null)
				throw new ArgumentNullException(nameof(macrotext));

			return _joinableTaskFactory.Run(() => ExecuteGuiMacroTextAsync(macrotext));
		}

		/// <summary>
		///     <para>Gets the exit code of the console process, if <see cref="IsConsoleProcessExited">it has already exited</see>. Throws an exception if it has not.</para>
		///     <para>This state only changes on the main thread.</para>
		/// </summary>
		public int GetConsoleProcessExitCode()
		{
			int? nCode = _nConsoleProcessExitCode;
			if(!nCode.HasValue)
				throw new InvalidOperationException("The exit code is not available yet because the console process is still running.");
			return nCode.Value;
		}

		/// <summary>
		///     <para>Kills the console process running in the console emulator window, if it has not exited yet.</para>
		///     <para>This does not necessarily kill the console emulator process which displays the console window, but it might also close if <see cref="ConEmuStartInfo.WhenConsoleProcessExits" /> says so.</para>
		/// </summary>
		/// <returns>Whether the process were killed (otherwise it has been terminated due to some other reason, e.g. exited on its own or killed by a third party).</returns>
		[NotNull]
		public async Task<bool> KillConsoleProcessAsync()
		{
			try
			{
				if((!_process.HasExited) && (!_nConsoleProcessExitCode.HasValue))
				{
					GetInfoRoot rootinfo = await GetInfoRoot.QueryAsync(this);
					if(!rootinfo.Pid.HasValue)
						return false; // Has already exited
					try
					{
						Process.GetProcessById((int)rootinfo.Pid.Value).Kill();
						return true;
					}
					catch(Exception)
					{
						// Most likely, has already exited
					}
				}
			}
			catch(Exception)
			{
				// Might be a race, so in between HasExited and Kill state could change, ignore possible errors here
			}
			return false;
		}

		/// <summary>
		///     <para>Sends the <c>Control+Break</c> signal to the console process, which will most likely abort it.</para>
		///     <para>Unlike <see cref="KillConsoleProcessAsync" />, this is a soft signal which might be processed by the console process for a graceful shutdown, or ignored altogether.</para>
		/// </summary>
		public Task SendControlBreakAsync()
		{
			try
			{
				if(!_process.HasExited)
					return BeginGuiMacro("Break").WithParam(1 /* Ctrl+Break */).ExecuteAsync();
			}
			catch(Exception)
			{
				// Might be a race, so in between HasExited and Kill state could change, ignore possible errors here
			}
			return Task.CompletedTask;
		}

		/// <summary>
		///     <para>Sends the <c>Control+C</c> signal to the payload console process, which will most likely abort it.</para>
		///     <para>Unlike <see cref="KillConsoleProcessAsync" />, this is a soft signal which might be processed by the console process for a graceful shutdown, or ignored altogether.</para>
		/// </summary>
		public Task SendControlCAsync()
		{
			try
			{
				if(!_process.HasExited)
					return BeginGuiMacro("Break").WithParam(0 /* Ctrl+C */).ExecuteAsync();
			}
			catch(Exception)
			{
				// Might be a race, so in between HasExited and Kill state could change, ignore possible errors here
			}
			return Task.CompletedTask;
		}

		/// <summary>
		///     <para>Waits until the console emulator closes and the console emulator view gets hidden from the control, or completes immediately if it has already exited.</para>
		///     <para>Note that the console process might have terminated long before this moment without closing the console emulator unless <see cref="WhenConsoleProcessExits.CloseConsoleEmulator" /> were selected in the startup options.</para>
		/// </summary>
		[NotNull]
		public Task WaitForConsoleEmulatorCloseAsync()
		{
			return _taskConsoleEmulatorClosed.Task;
		}

		/// <summary>
		///     <para>Waits for the console process running in the console emulator to terminate, or completes immediately if it has already terminated.</para>
		///     <para>If not <see cref="WhenConsoleProcessExits.CloseConsoleEmulator" />, the console emulator stays, otherwise it closes also, and the console emulator window is hidden from the control.</para>
		/// </summary>
		[NotNull]
		public Task<ConsoleProcessExitedEventArgs> WaitForConsoleProcessExitAsync()
		{
			return _taskConsoleProcessExit.Task;
		}

		/// <summary>
		///     <para>Writes text to the console input, as if it's been typed by user on the keyboard.</para>
		///     <para>Whether this will be visible (=echoed) on screen is up to the running console process.</para>
		/// </summary>
		public Task WriteInputTextAsync([NotNull] string text)
		{
			if(text == null)
				throw new ArgumentNullException(nameof(text));
			if(text.Length == 0)
				return Task.CompletedTask;

			return BeginGuiMacro("Paste").WithParam(2).WithParam(text).ExecuteAsync();
		}

		/// <summary>
		///     <para>Writes text to the console output, as if the current running console process has written it to stdout.</para>
		///     <para>Use with caution, as this might interfere with console process output in an unpredictable manner.</para>
		/// </summary>
		public Task WriteOutputTextAsync([NotNull] string text)
		{
			if(text == null)
				throw new ArgumentNullException(nameof(text));
			if(text.Length == 0)
				return Task.CompletedTask;

			return BeginGuiMacro("Write").WithParam(text).ExecuteAsync();
		}

		/// <summary>
		///     <para>Fires on the main thread when the console process writes into its output or error streams. Gets a chunk of the raw ANSI stream contents.</para>
		///     <para>For processes which write immediately on startup, this event might fire some chunks before you can start sinking it. To get notified reliably, use <see cref="ConEmuStartInfo.AnsiStreamChunkReceivedEventSink" />.</para>
		///     <para>To enable sinking this event, you must have <see cref="ConEmuStartInfo.IsReadingAnsiStream" /> set to <c>True</c> before starting the console process.</para>
		///     <para>If you're reading the ANSI log with <see cref="AnsiStreamChunkReceived" />, it's guaranteed that all the events for the log will be fired before <see cref="ConsoleProcessExited" />, and there will be no events afterwards.</para>
		/// </summary>
		[SuppressMessage("ReSharper", "DelegateSubtraction")]
		public event EventHandler<AnsiStreamChunkEventArgs> AnsiStreamChunkReceived
		{
			add
			{
				if(_ansilog == null)
					throw new InvalidOperationException("You cannot receive the ANSI stream data because the console process has not been set up to read the ANSI stream before running; set ConEmuStartInfo::IsReadingAnsiStream to True before starting the process.");
				_ansilog.AnsiStreamChunkReceived += value;
			}
			remove
			{
				if(_ansilog == null)
					throw new InvalidOperationException("You cannot receive the ANSI stream data because the console process has not been set up to read the ANSI stream before running; set ConEmuStartInfo::IsReadingAnsiStream to True before starting the process.");
				_ansilog.AnsiStreamChunkReceived -= value;
			}
		}

		/// <summary>
		///     <para>Fires on the main thread when the console emulator closes and the console emulator window is hidden from the control.</para>
		///     <para>Note that the console process might have terminated long before this moment without closing the console emulator unless <see cref="WhenConsoleProcessExits.CloseConsoleEmulator" /> were selected in the startup options.</para>
		///     <para>For short-lived processes, this event might fire before you can start sinking it. To get notified reliably, use <see cref="WaitForConsoleEmulatorCloseAsync" /> or <see cref="ConEmuStartInfo.ConsoleEmulatorClosedEventSink" />.</para>
		/// </summary>
		[CanBeNull]
		public event EventHandler ConsoleEmulatorClosed;

		/// <summary>
		///     <para>Fires on the main thread when the console process running in the console emulator terminates.</para>
		///     <para>If not <see cref="WhenConsoleProcessExits.CloseConsoleEmulator" />, the console emulator stays, otherwise it closes also, and the console emulator window is hidden from the control.</para>
		///     <para>For short-lived processes, this event might fire before you can start sinking it. To get notified reliably, use <see cref="WaitForConsoleProcessExitAsync" /> or <see cref="ConEmuStartInfo.ConsoleProcessExitedEventSink" />.</para>
		///     <para>If you're reading the ANSI log with <see cref="AnsiStreamChunkReceived" />, it's guaranteed that all the events for the log will be fired before <see cref="ConsoleProcessExited" />, and there will be no events afterwards.</para>
		/// </summary>
		public event EventHandler<ConsoleProcessExitedEventArgs> ConsoleProcessExited;

		[NotNull]
		private AnsiLog Init_AnsiLog([NotNull] ConEmuStartInfo startinfo)
		{
			var ansilog = new AnsiLog(_dirTempWorkingFolder);
			_lifetime.Add(() => ansilog.Dispose());
			if(startinfo.AnsiStreamChunkReceivedEventSink != null)
				ansilog.AnsiStreamChunkReceived += startinfo.AnsiStreamChunkReceivedEventSink;

			// Do the pumping periodically (TODO: take this to async?.. but would like to keep the final evt on the home thread, unless we go to tasks)
			// TODO: if ConEmu writes to a pipe, we might be getting events when more data comes to the pipe rather than poll it by timer
			var timer = new Timer() {Interval = (int)TimeSpan.FromSeconds(.1).TotalMilliseconds, Enabled = true};
			timer.Tick += delegate { ansilog.PumpStream(); };
			_lifetime.Add(() => timer.Dispose());

			return ansilog;
		}

		/// <summary>
		/// Watches for the status of the payload console process to fetch its exitcode when done and notify user of that.
		/// </summary>
		private void Init_ConsoleProcessMonitoring()
		{
			// When the payload process exits, use its exit code
			_joinableTaskFactory.RunAsync(async () =>
			{
				// Detect when this happens
				int? consoleProcessExitCode = await Init_PayloadProcessMonitoring_WaitForExitCodeAsync();

				await _joinableTaskFactory.SwitchToMainThreadAsync();

				if(!consoleProcessExitCode.HasValue) // Means the wait were aborted, e.g. ConEmu has been shut down and we processed that on the main thread
					return;
				TryFireConsoleProcessExited(consoleProcessExitCode.Value);
			});
		}

		[NotNull]
		private static unsafe CommandLineBuilder Init_MakeConEmuCommandLine([NotNull] ConEmuStartInfo startinfo, [NotNull] HostContext hostcontext, [CanBeNull] AnsiLog ansilog, [NotNull] DirectoryInfo dirLocalTempRoot)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(hostcontext == null)
				throw new ArgumentNullException(nameof(hostcontext));

			var cmdl = new CommandLineBuilder();

			// This sets up hosting of ConEmu in our control
			cmdl.AppendSwitch("-InsideWnd");
			cmdl.AppendFileNameIfNotNull("0x" + ((ulong)hostcontext.HWndParent).ToString("X"));

			// Don't use keyboard hooks in ConEmu when embedded
			cmdl.AppendSwitch("-NoKeyHooks");

			switch (startinfo.LogLevel)
			{
				case ConEmuStartInfo.LogLevels.Basic:
					cmdl.AppendSwitch("-Log"); break;
				case ConEmuStartInfo.LogLevels.Detailed:
					cmdl.AppendSwitch("-Log2"); break;
				case ConEmuStartInfo.LogLevels.Advanced:
					cmdl.AppendSwitch("-Log3"); break;
				case ConEmuStartInfo.LogLevels.Full:
					cmdl.AppendSwitch("-Log4"); break;
			}

			// Basic settings, like fonts and hidden tab bar
			// Plus some of the properties on this class
			cmdl.AppendSwitch("-LoadCfgFile");
			cmdl.AppendFileNameIfNotNull(Init_MakeConEmuCommandLine_EmitConfigFile(dirLocalTempRoot, startinfo, hostcontext));

			if(!string.IsNullOrEmpty(startinfo.StartupDirectory))
			{
				cmdl.AppendSwitch("-Dir");
				cmdl.AppendFileNameIfNotNull(startinfo.StartupDirectory);
			}

			// ANSI Log file
			if(ansilog != null)
			{
				cmdl.AppendSwitch("-AnsiLog");
				cmdl.AppendFileNameIfNotNull(ansilog.Directory.FullName);
			}
			if(dirLocalTempRoot == null)
				throw new ArgumentNullException(nameof(dirLocalTempRoot));

			// This one MUST be the last switch
			cmdl.AppendSwitch("-cmd");

			// Console mode command
			// NOTE: if placed AFTER the payload command line, otherwise somehow conemu hooks won't fetch the switch out of the cmdline, e.g. with some complicated git fetch/push cmdline syntax which has a lot of colons inside on itself
			string sConsoleExitMode;
			switch(startinfo.WhenConsoleProcessExits)
			{
			case WhenConsoleProcessExits.CloseConsoleEmulator:
				sConsoleExitMode = "n";
				break;
			case WhenConsoleProcessExits.KeepConsoleEmulator:
				sConsoleExitMode = "c0";
				break;
			case WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage:
				sConsoleExitMode = "c";
				break;
			default:
				throw new ArgumentOutOfRangeException("ConEmuStartInfo" + "::" + "WhenConsoleProcessExits", startinfo.WhenConsoleProcessExits, "This is not a valid enum value.");
			}
			cmdl.AppendSwitchIfNotNull("-cur_console:", $"{(startinfo.IsElevated ? "a" : "")}{sConsoleExitMode}");

			if (!string.IsNullOrEmpty(startinfo.ConsoleProcessExtraArgs))
			{
				cmdl.AppendSwitch(startinfo.ConsoleProcessExtraArgs);
			}

			// And the shell command line itself
			cmdl.AppendSwitch(startinfo.ConsoleProcessCommandLine);

			return cmdl;
		}

		private static string Init_MakeConEmuCommandLine_EmitConfigFile([NotNull] DirectoryInfo dirForConfigFile, [NotNull] ConEmuStartInfo startinfo, [NotNull] HostContext hostcontext)
		{
			if(dirForConfigFile == null)
				throw new ArgumentNullException(nameof(dirForConfigFile));
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(hostcontext == null)
				throw new ArgumentNullException(nameof(hostcontext));

			// Take baseline settings from the startinfo
			XmlDocument xmlBase = startinfo.BaseConfiguration;
			if(xmlBase.DocumentElement == null)
				throw new InvalidOperationException("The BaseConfiguration parameter of the ConEmuStartInfo must be a non-empty XmlDocument. This one does not have a root element.");
			if(xmlBase.DocumentElement.Name != ConEmuConstants.XmlElementKey)
				throw new InvalidOperationException($"The BaseConfiguration parameter of the ConEmuStartInfo must be an XmlDocument with the root element named “{ConEmuConstants.XmlElementKey}” in an empty namespace. The actual element name is “{xmlBase.DocumentElement.Name}”.");
			if(!String.Equals(xmlBase.DocumentElement.GetAttribute(ConEmuConstants.XmlAttrName), ConEmuConstants.XmlValueSoftware, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException($"The BaseConfiguration parameter of the ConEmuStartInfo must be an XmlDocument whose root element is named “{ConEmuConstants.XmlElementKey}” and has an attribute “{ConEmuConstants.XmlAttrName}” set to “{ConEmuConstants.XmlValueSoftware}”. The actual value of this attribute is “{xmlBase.DocumentElement.GetAttribute(ConEmuConstants.XmlAttrName)}”.");

			// Load default template
			var xmldoc = new XmlDocument();
			xmldoc.AppendChild(xmldoc.ImportNode(xmlBase.DocumentElement, true));

			// Ensure the settings file has the expected keys structure
			// As we now allow user-supplied documents, we must ensure these elements exist
			XmlElement xmlSoftware = xmldoc.DocumentElement;
			if(xmlSoftware == null)
				throw new InvalidOperationException("Not expecting the cloned element to be NULL.");
			var xmlConEmu = xmlSoftware.SelectSingleNode($"{ConEmuConstants.XmlElementKey}[@{ConEmuConstants.XmlAttrName}='{ConEmuConstants.XmlValueConEmu}']") as XmlElement;
			if(xmlConEmu == null)
			{
				xmlSoftware.AppendChild(xmlConEmu = xmldoc.CreateElement(ConEmuConstants.XmlElementKey));
				xmlConEmu.SetAttribute(ConEmuConstants.XmlAttrName, ConEmuConstants.XmlValueConEmu);
			}
			var xmlDotVanilla = xmlConEmu.SelectSingleNode($"{ConEmuConstants.XmlElementKey}[@{ConEmuConstants.XmlAttrName}='{ConEmuConstants.XmlValueDotVanilla}']") as XmlElement;
			if(xmlDotVanilla == null)
			{
				xmlConEmu.AppendChild(xmlDotVanilla = xmldoc.CreateElement(ConEmuConstants.XmlElementKey));
				xmlDotVanilla.SetAttribute(ConEmuConstants.XmlAttrName, ConEmuConstants.XmlValueDotVanilla);
			}

			// Apply settings from properties
			XmlNode xmlSettings = xmlDotVanilla;
			{
				string keyname = "StatusBar.Show";
				var xmlElem = ((XmlElement)(xmlSettings.SelectSingleNode($"value[@name='{keyname}']") ?? xmlSettings.AppendChild(xmldoc.CreateElement("value"))));
				xmlElem.SetAttribute(ConEmuConstants.XmlAttrName, keyname);
				xmlElem.SetAttribute("type", "hex");
				xmlElem.SetAttribute("data", (hostcontext.IsStatusbarVisibleInitial ? 1 : 0).ToString());
			}

			// Environment variables
			if((startinfo.EnumEnv().Any()) || (startinfo.IsEchoingConsoleCommandLine) || (startinfo.GreetingText.Length > 0))
			{
				string keyname = "EnvironmentSet";
				var xmlElem = ((XmlElement)(xmlSettings.SelectSingleNode($"value[@name='{keyname}']") ?? xmlSettings.AppendChild(xmldoc.CreateElement("value"))));
				xmlElem.SetAttribute(ConEmuConstants.XmlAttrName, keyname);
				xmlElem.SetAttribute("type", "multi");
				foreach(string key in startinfo.EnumEnv())
				{
					XmlElement xmlLine;
					xmlElem.AppendChild(xmlLine = xmldoc.CreateElement("line"));
					xmlLine.SetAttribute("data", $"set {key}={startinfo.GetEnv(key)}");
				}

				// Echo the custom greeting text
				if(startinfo.GreetingText.Length > 0)
				{
					// Echo each line separately
					List<string> lines = Regex.Split(startinfo.GreetingText, @"\r\n|\n|\r").ToList();
					if((lines.Any()) && (lines.Last().Length == 0)) // Newline handling, as declared
						lines.RemoveAt(lines.Count - 1);
					foreach(string line in lines)
					{
						XmlElement xmlLine;
						xmlElem.AppendChild(xmlLine = xmldoc.CreateElement("line"));
						xmlLine.SetAttribute("data", $"echo {Init_MakeConEmuCommandLine_EmitConfigFile_EscapeEchoText(line)}");
					}
				}

				// To echo the cmdline, add an echo command to the env-init session
				if(startinfo.IsEchoingConsoleCommandLine)
				{
					XmlElement xmlLine;
					xmlElem.AppendChild(xmlLine = xmldoc.CreateElement("line"));
					xmlLine.SetAttribute("data", $"echo {Init_MakeConEmuCommandLine_EmitConfigFile_EscapeEchoText(startinfo.ConsoleProcessCommandLine)}");
				}
			}

			// Write out to temp location
			dirForConfigFile.Create();
			string sConfigFile = Path.Combine(dirForConfigFile.FullName, "Config.Xml");
			xmldoc.Save(sConfigFile);
			new FileInfo(sConfigFile).IsReadOnly = true; // Mark the file as readonly, so that ConEmu didn't suggest to save modifications to this temp file

			return sConfigFile;
		}

		/// <summary>
		/// Applies escaping so that (1) it went as a single argument into the ConEmu's <c>NextArg</c> function; (2) its special chars were escaped according to the ConEmu's <c>DoOutput</c> function which implements this echo.
		/// </summary>
		private static string Init_MakeConEmuCommandLine_EmitConfigFile_EscapeEchoText([NotNull] string text)
		{
			if(text == null)
				throw new ArgumentNullException(nameof(text));

			var sb = new StringBuilder(text.Length + 2);

			// We'd always quote the arg; no harm, and works better with an empty string
			sb.Append('"');

			foreach(char ch in text)
			{
				switch(ch)
				{
				case '"':
					sb.Append('"').Append('"'); // Quotes are doubled in this format
					break;
				case '^':
					sb.Append("^^");
					break;
				case '\r':
					sb.Append("^R");
					break;
				case '\n':
					sb.Append("^N");
					break;
				case '\t':
					sb.Append("^T");
					break;
				case '\x7':
					sb.Append("^A");
					break;
				case '\b':
					sb.Append("^B");
					break;
				case '[':
					sb.Append("^E");
					break;
				default:
					sb.Append(ch);
					break;
				}
			}

			// Close arg quoting
			sb.Append('"');

			return sb.ToString();
		}

		/// <summary>
		/// Async-loop retries for getting the root payload process to await its exit.
		/// </summary>
		private async Task<int?> Init_PayloadProcessMonitoring_WaitForExitCodeAsync()
		{
			for(;;)
			{
				// Might have been terminated on the main thread
				if(_nConsoleProcessExitCode.HasValue)
					return null;
				if(_process.HasExited)
					return null;

				try
				{
					// Ask ConEmu for PID
					GetInfoRoot rootinfo = await GetInfoRoot.QueryAsync(this);

					// Check if the process has extied, then we're done
					if(rootinfo.ExitCode.HasValue)
						return rootinfo.ExitCode.Value;

					// If it has started already, must get a PID
					// Await till the process exits and loop to reask conemu for its result
					// If conemu exits too in this time, then it will republish payload exit code as its own exit code, and implementation will use it
					if(rootinfo.Pid.HasValue)
					{
						await WinApi.Helpers.WaitForProcessExitAsync(rootinfo.Pid.Value);
						continue; // Do not wait before retrying
					}
				}
				catch(Exception)
				{
					// Smth failed, wait and retry
				}

				// Await before retrying once more
				await Task.Delay(TimeSpan.FromMilliseconds(10));
			}
		}

		[NotNull]
		private Process Init_StartConEmu([NotNull] ConEmuStartInfo startinfo, [NotNull] CommandLineBuilder cmdl)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(cmdl == null)
				throw new ArgumentNullException(nameof(cmdl));

			try
			{
				if(string.IsNullOrEmpty(startinfo.ConEmuExecutablePath))
					throw new InvalidOperationException("Could not run the console emulator. The path to ConEmu.exe could not be detected.");
				if(!File.Exists(startinfo.ConEmuExecutablePath))
					throw new InvalidOperationException($"Missing ConEmu executable at location “{startinfo.ConEmuExecutablePath}”.");
				var processNew = new Process() {StartInfo = new ProcessStartInfo(startinfo.ConEmuExecutablePath, cmdl.ToString()) {UseShellExecute = false}};

				// Bind process termination
				processNew.EnableRaisingEvents = true;
				processNew.Exited += delegate
				{
					// Ensure STA
					_joinableTaskFactory.RunAsync(async () =>
					{
						await _joinableTaskFactory.SwitchToMainThreadAsync();

						// Tear down all objects
						TerminateLifetime();

						// If we haven't separately caught an exit of the payload process
						TryFireConsoleProcessExited(_process.ExitCode /* We haven't caught the exit of the payload process, so we haven't gotten a message with its errorlevel as well. Assume ConEmu propagates its exit code, as there ain't other way for getting it now */);

						// Fire client total exited event
						ConsoleEmulatorClosed?.Invoke(this, EventArgs.Empty);
					});
				};

				if(!processNew.Start())
					throw new Win32Exception("The process did not start.");
				return processNew;
			}
			catch(Win32Exception ex)
			{
				TerminateLifetime();
				throw new InvalidOperationException("Could not run the console emulator. " + ex.Message + $" ({ex.NativeErrorCode:X8})" + Environment.NewLine + Environment.NewLine + "Command:" + Environment.NewLine + startinfo.ConEmuExecutablePath + Environment.NewLine + Environment.NewLine + "Arguments:" + Environment.NewLine + cmdl, ex);
			}
		}

		[NotNull]
		private DirectoryInfo Init_TempWorkingFolder()
		{
			var _dirTempWorkingDir = new DirectoryInfo(Path.Combine(Path.Combine(Path.GetTempPath(), "ConEmu"), $"{DateTime.UtcNow.ToString("s").Replace(':', '-')}.{Process.GetCurrentProcess().Id:X8}.{unchecked((uint)RuntimeHelpers.GetHashCode(this)):X8}")); // Prefixed with date-sortable; then PID; then sync table id of this object

			_lifetime.Add(() =>
			{
				try
				{
					if(_dirTempWorkingDir.Exists)
						_dirTempWorkingDir.Delete(true);
				}
				catch(Exception)
				{
					// Not interested
				}
			});

			return _dirTempWorkingDir;
		}

		private void Init_WireEvents([NotNull] ConEmuStartInfo startinfo)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));

			// Advise events before they got chance to fire, use event sinks from startinfo for guaranteed delivery
			if(startinfo.ConsoleProcessExitedEventSink != null)
				ConsoleProcessExited += startinfo.ConsoleProcessExitedEventSink;
			if(startinfo.ConsoleEmulatorClosedEventSink != null)
				ConsoleEmulatorClosed += startinfo.ConsoleEmulatorClosedEventSink;

			// Re-issue events as async tasks
			// As we advise events before they even fire, the task is guaranteed to get its state
			ConsoleProcessExited += (sender, args) => _taskConsoleProcessExit.SetResult(args);
			ConsoleEmulatorClosed += delegate { _taskConsoleEmulatorClosed.SetResult(Missing.Value); };
		}

		private void TerminateLifetime()
		{
			List<Action> items = _lifetime.ToList();
			_lifetime.Clear();
			items.Reverse();
			foreach(Action item in items)
				item();
		}

		/// <summary>
		/// Fires the payload exited event if it has not been fired yet.
		/// </summary>
		/// <param name="nConsoleProcessExitCode"></param>
		private void TryFireConsoleProcessExited(int nConsoleProcessExitCode)
		{
			if(_nConsoleProcessExitCode.HasValue) // It's OK to call it from multiple places, e.g. when payload exit were detected and when ConEmu process itself exits
				return;

			// Make sure the whole ANSI log contents is pumped out before we notify user
			// Dispose call pumps all out and makes sure we never ever fire anything on it after we notify user of ConsoleProcessExited; multiple calls to Dispose are OK
			_ansilog?.Dispose();

			// Store exit code
			_nConsoleProcessExitCode = nConsoleProcessExitCode;

			// Notify user
			ConsoleProcessExited?.Invoke(this, new ConsoleProcessExitedEventArgs(nConsoleProcessExitCode));
		}

		/// <summary>
		/// Covers parameters of the host control needed to run the session. <see cref="ConEmuStartInfo" /> tells what to run and how, while this class tells “where” and is not directly user-configurable, it's derived from the hosting control.
		/// </summary>
		public unsafe class HostContext
		{
			public HostContext([NotNull] void* hWndParent, bool isStatusbarVisibleInitial)
			{
				if(hWndParent == null)
					throw new ArgumentNullException(nameof(hWndParent));
				HWndParent = hWndParent;
				IsStatusbarVisibleInitial = isStatusbarVisibleInitial;
			}

			[NotNull]
			public readonly void* HWndParent;

			public readonly bool IsStatusbarVisibleInitial;
		}
	}
}
