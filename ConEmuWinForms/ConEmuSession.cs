using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace ConEmu.WinForms
{
	/// <summary>
	/// A single session of the console emulator run. Each top-level command execution spawns a new session.
	/// </summary>
	public unsafe class ConEmuSession
	{
		EventHandler<AnsiStreamChunkEventArgs> _ansiStreamChunkReceived;

		[NotNull]
		private readonly DirectoryInfo _dirLocalTempRoot;

		[NotNull]
		private readonly WindowsFormsSynchronizationContext _dispatcher;

		bool _isPayloadExited;

		/// <summary>
		/// The ConEmu process, even after it exits.
		/// </summary>
		[NotNull]
		private readonly Process _process;

		[NotNull]
		private readonly ConEmuStartInfo _startinfo;

		public ConEmuSession([NotNull] ConEmuStartInfo startinfo, [NotNull] HostContext hostcontext)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(hostcontext == null)
				throw new ArgumentNullException(nameof(hostcontext));
			_startinfo = startinfo;
			startinfo.MarkAsUsedUp();

			_dispatcher = new WindowsFormsSynchronizationContext();
			_dirLocalTempRoot = new DirectoryInfo(Path.Combine(Path.Combine(Path.GetTempPath(), "ConEmu"), $"{DateTime.UtcNow.ToString("s").Replace(':', '-')}.{Process.GetCurrentProcess().Id:X8}.{unchecked((uint)RuntimeHelpers.GetHashCode(this)):X8}")); // Prefixed with date-sortable; then PID; then sync table id of this object

			if(string.IsNullOrEmpty(startinfo.ConsoleCommandLine))
				throw new InvalidOperationException("Cannot start a new console process for command line “{0}” because it's either NULL, or empty, or whitespace.");

			// Events wiring
			if(startinfo.AnsiStreamChunkReceivedEventSink != null)
				_ansiStreamChunkReceived += startinfo.AnsiStreamChunkReceivedEventSink;
			if(startinfo.PayloadExitedEventSink != null)
				PayloadExited += startinfo.PayloadExitedEventSink;
			if(startinfo.ConsoleEmulatorExitedEventSink != null)
				ConsoleEmulatorExited += startinfo.ConsoleEmulatorExitedEventSink;

			var cmdl = new CommandLineBuilder();

			// This sets up hosting of ConEmu in our control
			cmdl.AppendSwitch("-InsideWnd");
			cmdl.AppendFileNameIfNotNull("0x" + ((ulong)hostcontext.HWndParent).ToString("X"));

			// Don't use keyboard hooks in ConEmu when embedded
			cmdl.AppendSwitch("-NoKeyHooks");

			// Basic settings, like fonts and hidden tab bar
			// Plus some of the properties on this class
			cmdl.AppendSwitch("-LoadCfgFile");
			cmdl.AppendFileNameIfNotNull(EmitConfigFile(_dirLocalTempRoot, startinfo, hostcontext));

			if(!string.IsNullOrEmpty(startinfo.StartupDirectory))
			{
				cmdl.AppendSwitch("-Dir");
				cmdl.AppendFileNameIfNotNull(startinfo.StartupDirectory);
			}

			// Write console buffer output to a file
			DirectoryInfo dirAnsiLog = null;
			if(startinfo.IsReadingAnsiStream)
			{
				dirAnsiLog = _dirLocalTempRoot;
				dirAnsiLog.Create();

				// Submit to ConEmu
				cmdl.AppendSwitch("-AnsiLog");
				cmdl.AppendFileNameIfNotNull(dirAnsiLog.FullName);
			}

			// This one MUST be the last switch
			cmdl.AppendSwitch("-cmd");

			// Console mode command
			// NOTE: if placed AFTER the payload command line, otherwise somehow conemu hooks won't fetch the switch out of the cmdline, e.g. with some complicated git fetch/push cmdline syntax which has a lot of colons inside on itself
			cmdl.AppendSwitchIfNotNull("-cur_console:", $"{(startinfo.IsElevated ? "a" : "")}{(startinfo.IsKeepingTerminalOnCommandExit ? "c" : "")}");

			// And the shell command line itself
			cmdl.AppendSwitch(startinfo.ConsoleCommandLine);

			if(string.IsNullOrEmpty(startinfo.ConEmuExecutablePath))
				throw new InvalidOperationException("Could not run the console emulator. The path to ConEmu.exe could not be detected.");

			EventHandler evtPolling = null;
			EventHandler evtCleanupOnExit = null;

			// Start ConEmu
			try
			{
				if(!File.Exists(startinfo.ConEmuExecutablePath))
					throw new InvalidOperationException($"Missing ConEmu executable at location “{startinfo.ConEmuExecutablePath}”.");
				var processNew = new Process() {StartInfo = new ProcessStartInfo(startinfo.ConEmuExecutablePath, cmdl.ToString()) {UseShellExecute = false}};

				// Bind process termination
				processNew.EnableRaisingEvents = true;
				processNew.Exited += delegate
				{
					_dispatcher.Post(o => // Ensure STA
					{
#pragma warning disable once AccessToModifiedClosure
						evtCleanupOnExit?.Invoke(this, EventArgs.Empty);

						// Notify client on the proper thread
						if(!_isPayloadExited)
						{
							_isPayloadExited = true;
							PayloadExited?.Invoke(this, EventArgs.Empty);
						}
						ConsoleEmulatorExited?.Invoke(this, EventArgs.Empty);
					}, null);
				};

				if(!processNew.Start())
					throw new Win32Exception("The process did not start.");
				_process = processNew;
			}
			catch(Win32Exception ex)
			{
				throw new InvalidOperationException("Could not run the console emulator. " + ex.Message + $" ({ex.NativeErrorCode:X8})" + Environment.NewLine + Environment.NewLine + "Command:" + Environment.NewLine + startinfo.ConEmuExecutablePath + Environment.NewLine + Environment.NewLine + "Arguments:" + Environment.NewLine + cmdl, ex);
			}

			// TODO: for now, this will be a polling timer, consider free-threaded treatment later on
			var timer = new Timer() {Interval = (int)TimeSpan.FromSeconds(.1).TotalMilliseconds, Enabled = true};
			timer.Tick += delegate
			{
				evtPolling?.Invoke(this, EventArgs.Empty);
				if(_process.HasExited)
				{
					evtPolling = null; // Timer might fire a few more fake events on disposal
					timer.Dispose();
				}
			};
			evtCleanupOnExit += delegate { timer.Dispose(); };

			// Attach input file
			if(dirAnsiLog != null)
			{
				// NOTE: can't run GUI macro immediately
				//				BeginGuiMacro("GetInfo").WithParam("AnsiLog").Execute(result => MessageBox.Show(result.Response));

				// A function which processes the part of the stream which gets available (or does the rest of it at the end)
				// TODO: try managing memory traffic
				FileInfo fiLog = null;
				FileStream fstream = null;
				Action FPumpStream = () =>
				{
					if(fiLog == null)
						fiLog = dirAnsiLog.GetFiles("ConEmu*.log").FirstOrDefault();
					if((fstream == null) && (fiLog != null))
						fstream = fiLog.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

					if(fstream != null)
					{
						long length = fstream.Length;
						if(fstream.Position < length)
						{
							var buffer = new byte[length - fstream.Position]; // TODO: buffer pooling
							int nRead = fstream.Read(buffer, 0, buffer.Length);
							if(nRead > 0)
							{
								// Buffer exactly for data
								AnsiStreamChunkEventArgs args;
								if(nRead < buffer.Length)
								{
									var subbuffer = new byte[nRead];
									Buffer.BlockCopy(buffer, 0, subbuffer, 0, nRead);
									args = new AnsiStreamChunkEventArgs(subbuffer);
								}
								else
									args = new AnsiStreamChunkEventArgs(buffer);

								// Fire
								_ansiStreamChunkReceived?.Invoke(this, args);
							}
						}
					}
				};

				// Do the pumping periodically (TODO: take this to async?.. but would like to keep the final evt on the home thread, unless we go to tasks)
				evtPolling += delegate { FPumpStream(); };
				evtCleanupOnExit += delegate
				{
					// Must pump out the rest of the stream
					FPumpStream();
					// Close the file if open
					fstream?.Dispose(); // Don't NULL it because this way it would throw in case we accidentally call Pump once more
				};
			}

			evtCleanupOnExit += delegate
			{
				// Cleanup any leftover temp files
				try
				{
					if(_dirLocalTempRoot.Exists)
						_dirLocalTempRoot.Delete(true);
				}
				catch(Exception)
				{
					// Not interested
				}
			};

			// Detect when payload process exits
			Process processWaitingFor = null; // We know the PID of the process running in the console, and are waiting for it to exit
			DateTime timeLastAskedForActivePid = DateTime.MinValue;
			evtPolling += delegate
			{
				if(_isPayloadExited)
					return;
				if(_process.HasExited)
					return;
				// We've seen an active process, check if it's exited
				if(processWaitingFor != null)
				{
					if(processWaitingFor.HasExited)
						processWaitingFor = null; // Means the former active process has exited, but we don't know if that were the top-level active process, could have been a child. So make a new request now and check if there's a new active process (or report exited if not)
				}

				// Don't know no active process, ask ConEmu on that matter
				if(processWaitingFor == null)
				{
					// Waiting for now process yet, ask the console
					if(DateTime.UtcNow - timeLastAskedForActivePid > TimeSpan.FromSeconds(1)) // Only if not pending a fresh question
					{
						timeLastAskedForActivePid = DateTime.UtcNow;
						BeginGuiMacro("GetInfo").WithParam("ActivePID").Execute(result =>
						{
							if(result.ErrorLevel != 0) // E.g. ConEmu has not fully started yet, and ConEmuC failed to connect to the instance — would usually return an error on the first call
								return;
							int nActivePid;
							if(!int.TryParse(result.Response, out nActivePid))
								return; // Not an int, that's not expected if errorlevel is 0
							if(nActivePid == 0)
							{
								// Means no process is running in ConEmu, all done
								_isPayloadExited = true;
								PayloadExited?.Invoke(this, EventArgs.Empty);
							}
							else
							{
								// Got a process, wait for this process now
								// Migth sink its Exited event (when we got Tasks here e.g.), but for now it's simpler to reuse the same polling timer
								try
								{
									processWaitingFor = Process.GetProcessById(nActivePid);
								}
								catch(Exception)
								{
									// Might be various access problems, or the process might have exited in between our getting its PID and attaching
								}
							}
						});
					}
				}
			};
		}

		public int ExitCode
		{
			get
			{
				if(!_process.HasExited)
					throw new InvalidOperationException("The exit code is not available yet because the process has not yet exited.");
				return _process.ExitCode;
			}
		}

		[NotNull]
		public ConEmuStartInfo StartInfo
		{
			get
			{
				return _startinfo;
			}
		}

		/// <summary>
		/// Starts construction of the ConEmu GUI Macro.
		/// </summary>
		[Pure]
		public GuiMacroBuilder BeginGuiMacro([NotNull] string sMacroName)
		{
			if(sMacroName == null)
				throw new ArgumentNullException(nameof(sMacroName));

			return new GuiMacroBuilder(this, sMacroName, Enumerable.Empty<string>());
		}

		/// <summary>
		/// Executes a ConEmu GUI Macro on the active console, see http://conemu.github.io/en/GuiMacro.html .
		/// </summary>
		/// <param name="macrotext">The full macro command, see http://conemu.github.io/en/GuiMacro.html .</param>
		/// <param name="FWhenDone">Optional. Executes on the same thread when the macro is done executing.</param>
		public void ExecuteGuiMacroText([NotNull] string macrotext, [CanBeNull] Action<GuiMacroResult> FWhenDone = null)
		{
			if(macrotext == null)
				throw new ArgumentNullException(nameof(macrotext));

			Process processConEmu = _process;
			if(processConEmu == null)
				throw new InvalidOperationException("Cannot execute a macro because the console process is not running at the moment.");

			// conemuc.exe -silent -guimacro:1234 print("\e","git"," --version","\n")
			var cmdl = new CommandLineBuilder();
			cmdl.AppendSwitch("-silent");
			cmdl.AppendSwitchIfNotNull("-GuiMacro:", processConEmu.Id.ToString());
			cmdl.AppendSwitch(macrotext /* appends the text unquoted for cmdline */);

			string exe = _startinfo.ConEmuConsoleExtenderExecutablePath;
			if(exe == "")
				throw new InvalidOperationException("The ConEmu Console Extender Executable is not available.");
			if(!File.Exists(exe))
				throw new InvalidOperationException($"The ConEmu Console Extender Executable does not exist on disk at “{exe}”.");

			try
			{
				var processExtender = new Process() {StartInfo = new ProcessStartInfo(exe, cmdl.ToString()) {WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false}};
				if(FWhenDone != null)
				{
					var sbResult = new StringBuilder();
					processExtender.EnableRaisingEvents = true;
					processExtender.Exited += delegate
					{
						GuiMacroResult result;
						lock(sbResult)
						{
							result = new GuiMacroResult() {ErrorLevel = processExtender.ExitCode, Response = sbResult.ToString()};
						}
						_dispatcher.Post(delegate { FWhenDone(result); }, null);
					};

					DataReceivedEventHandler FOnData = (sender, args) =>
					{
						lock(sbResult)
							sbResult.Append(args.Data);
					};
					processExtender.OutputDataReceived += FOnData;
					processExtender.ErrorDataReceived += FOnData;
				}
				processExtender.Start();
				processExtender.BeginOutputReadLine();
				processExtender.BeginErrorReadLine();
			}
			catch(Exception ex)
			{
				throw new InvalidOperationException($"Could not run the ConEmu Console Extender Executable at “{exe}” with command-line arguments “{cmdl}”.", ex);
			}
		}

		/// <summary>
		/// Kills the whole console emulator process if it is running. This also terminates the console emulator window.	// TODO: kill payload process only when we know its pid
		/// </summary>
		public void KillConsoleEmulator()
		{
			try
			{
				if(!_process.HasExited)
					_process.Kill();
			}
			catch(Exception)
			{
				// Might be a race, so in between HasExited and Kill state could change, ignore possible errors here
			}
		}

		/// <summary>
		///     <para>Fires when the console process writes into its output or error stream. Gets a chunk of the raw ANSI stream contents.</para>
		///     <para>For processes which write immediately on startup, this event might fire some chunks before you sink it. To get notified reliably, use <see cref="ConEmuStartInfo.AnsiStreamChunkReceivedEventSink" />.</para>
		///     <para>To enable sinking this event, you must have <see cref="ConEmuStartInfo.IsReadingAnsiStream" /> set to <c>True</c> before starting the console process.</para>
		/// </summary>
		[SuppressMessage("ReSharper", "DelegateSubtraction")]
		public event EventHandler<AnsiStreamChunkEventArgs> AnsiStreamChunkReceived
		{
			add
			{
				if(!_startinfo.IsReadingAnsiStream)
					throw new InvalidOperationException("You cannot receive the ANSI stream data because the console process has not been set up to read the ANSI stream before running; set ConEmuStartInfo::IsReadingAnsiStream to True before starting the process.");
				_ansiStreamChunkReceived += value;
			}
			remove
			{
				_ansiStreamChunkReceived -= value;
			}
		}

		/// <summary>
		///     <para>Fires when the console emulator process exits and stops rendering the terminal view. Note that the root command might have had stopped running long before this moment if <see cref="ConEmuStartInfo.IsKeepingTerminalOnCommandExit" /> prevents terminating the terminal view immediately.</para>
		///     <para>For short-lived processes, this event might fire before you sink it. To get notified reliably, use <see cref="ConEmuStartInfo.ConsoleEmulatorExitedEventSink" />.</para>
		/// </summary>
		public event EventHandler ConsoleEmulatorExited;

		private static string EmitConfigFile([NotNull] DirectoryInfo dirForConfigFile, [NotNull] ConEmuStartInfo startinfo, [NotNull] HostContext hostcontext)
		{
			if(dirForConfigFile == null)
				throw new ArgumentNullException(nameof(dirForConfigFile));
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(hostcontext == null)
				throw new ArgumentNullException(nameof(hostcontext));
			// Load default template
			var xmldoc = new XmlDocument();
			xmldoc.Load(new MemoryStream(Resources.ConEmuSettingsTemplate));
			XmlNode xmlSettings = xmldoc.SelectSingleNode("/key[@name='Software']/key[@name='ConEmu']/key[@name='.Vanilla']");
			if(xmlSettings == null)
				throw new InvalidOperationException("Unexpected mismatch in XML resource structure.");

			// Apply settings from properties
			{
				var xmlElem = ((XmlElement)(xmlSettings.SelectSingleNode("value[@name='StatusBar.Show']") ?? xmlSettings.AppendChild(xmldoc.CreateElement("value"))));
				xmlElem.SetAttribute("type", "hex");
				xmlElem.SetAttribute("data", (hostcontext.IsStatusbarVisibleInitial ? 1 : 0).ToString());
			}

			// Environment variables
			if(startinfo.EnumEnv().Any())
			{
				var xmlElem = ((XmlElement)(xmlSettings.SelectSingleNode("value[@name='EnvironmentSet']") ?? xmlSettings.AppendChild(xmldoc.CreateElement("value"))));
				xmlElem.SetAttribute("type", "multi");
				foreach(string key in startinfo.EnumEnv())
				{
					XmlElement xmlLine;
					xmlElem.AppendChild(xmlLine = xmldoc.CreateElement("line"));
					xmlLine.SetAttribute("data", $"set {key}={startinfo.GetEnv(key)}");
				}
			}

			// Write out to temp location
			dirForConfigFile.Create();
			string sConfigFile = Path.Combine(dirForConfigFile.FullName, "Config.Xml");
			xmldoc.Save(sConfigFile);

			return sConfigFile;
		}

		/// <summary>
		///     <para>Fires when the payload command exits within the terminal. If <see cref="ConEmuStartInfo.IsKeepingTerminalOnCommandExit" />, the terminal stays, otherwise it closes also.</para>
		///     <para>For short-lived processes, this event might fire before you sink it. To get notified reliably, use <see cref="ConEmuStartInfo.PayloadExitedEventSink" />.</para>
		/// </summary>
		public event EventHandler PayloadExited;

		public class HostContext
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