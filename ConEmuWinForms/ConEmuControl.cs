using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace ConEmu.WinForms
{
	/// <summary>
	///     <para>The console emulator control.</para>
	///     <para>If <see cref="IsStartingImmediately" />, immediately runs the command in <see cref="ConsoleCommandLine" /> and shows the console emulator in the control. Otherwise (or after the process exits), shows the gray background.</para>
	/// </summary>
	public unsafe class ConEmuControl : Control
	{
		[NotNull]
		static readonly string ConEmuConsoleExtenderExeName = "ConEmuC.exe";

		[NotNull]
		static readonly string ConEmuExeName = "conemu.exe";

		[NotNull]
		private static readonly string ConEmuSubfolderName = "ConEmu";

		readonly IDictionary<string, string> _environment = new Dictionary<string, string>();

		private bool _isElevated;

		bool _isEverRun;

		private bool _isStartingImmediately = true;

		private bool _isStatusbarVisible = true;

		private int _nLastExitCode;

		/// <summary>
		/// The ConEmu process, when it's running; or <c>NULL</c> otherwise.
		/// </summary>
		[CanBeNull]
		private Process _process;

		[NotNull]
		private string _sConEmuConsoleExtenderExecutablePath = "";

		[NotNull]
		private string _sConEmuExecutablePath = "";

		/// <summary>
		/// Should clean up upon dispose.
		/// </summary>
		[CanBeNull]
		private string _sConEmuSettingsWrittenTempFile;

		private string _sConsoleCommandLine = "{cmd}"; /* this is the standard ConEmu task name for the console */

		public ConEmuControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.Selectable, true);
			ConEmuExecutablePath = InitConEmuLocation();
		}

		/// <summary>
		/// Gets or sets the path to the ConEmu console extender (<c>ConEmuC.exe</c>).
		/// Will be autodetected from the path to this DLL or from <see cref="ConEmuExecutablePath" /> if possible.
		/// </summary>
		[NotNull]
		public string ConEmuConsoleExtenderExecutablePath
		{
			get
			{
				return _sConEmuConsoleExtenderExecutablePath;
			}
			set
			{
				if(value == null)
					throw new ArgumentNullException(nameof(value));
				if((value == "") && (_sConEmuConsoleExtenderExecutablePath == ""))
					return;
				if(value == "")
					throw new ArgumentOutOfRangeException(nameof(value), value, "Cannot reset path to an empty string.");
				_sConEmuConsoleExtenderExecutablePath = value; // Delay existence check 'til we call it
			}
		}

		/// <summary>
		/// Gets or sets the path to the <c>ConEmu.exe</c> which will be the console emulator root process.
		/// Will be autodetected from the path to this DLL if possible.
		/// </summary>
		[NotNull]
		public string ConEmuExecutablePath
		{
			get
			{
				return _sConEmuExecutablePath;
			}
			set
			{
				if(value == null)
					throw new ArgumentNullException(nameof(value));
				if((value == "") && (_sConEmuExecutablePath == ""))
					return;
				if(value == "")
					throw new ArgumentOutOfRangeException(nameof(value), value, "Cannot reset path to an empty string.");
				_sConEmuExecutablePath = value; // Delay existence check 'til we call it

				if(_sConEmuConsoleExtenderExecutablePath == "")
					_sConEmuConsoleExtenderExecutablePath = TryDeriveConEmuConsoleExtenderExecutablePath(_sConEmuExecutablePath);
			}
		}

		/// <summary>
		/// The command line to execute in the console emulator on <see cref="Start" /> or when the control is created if <see cref="IsStartingImmediately" />.
		/// </summary>
		[NotNull]
		public string ConsoleCommandLine
		{
			get
			{
				return _sConsoleCommandLine;
			}
			set
			{
				AssertNotRunning();
				_sConsoleCommandLine = value;
			}
		}

		/// <summary>
		/// Gets the state of the console emulator.
		/// </summary>
		public States ConsoleState => _process != null ? States.Running : (_isEverRun ? States.Exited : States.Empty);

		/// <summary>
		/// Whether the console process is currently running.
		/// </summary>
		public bool IsConsoleProcessRunning => _process != null;

		/// <summary>
		/// Gets or sets whether the console process is to be run elevated (an elevation prompt will be shown).
		/// </summary>
		public bool IsElevated
		{
			get
			{
				return _isElevated;
			}
			set
			{
				AssertNotRunning();
				_isElevated = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets whether this control will start the console process as soon as it's loaded on the form.</para>
		///     <para>You can either specify the console executable to run in <see cref="ConsoleCommandLine" /> (the console window will close as soon as it exits), or use its default value for <c>COMSPEC</c> and execute your command in that console with <see cref="PasteText" /> (the console will remain operable after the command completes).</para>
		/// </summary>
		public bool IsStartingImmediately
		{
			get
			{
				return _isStartingImmediately;
			}
			set
			{
				AssertNotRunning();
				if(_isEverRun)
					throw new InvalidOperationException("IsStartingImmediately can only be changed before the first console process runs in this control.");
				_isStartingImmediately = value;

				// Invariant: if changed to TRUE past the normal IsStartingImmediately checking point
				if((value) && (IsHandleCreated))
					Start();
			}
		}

		public bool IsStatusbarVisible
		{
			get
			{
				return _isStatusbarVisible;
			}
			set
			{
				_isStatusbarVisible = value;
				if(_process != null)
					BeginGuiMacro("Status").WithParam(0).WithParam(value ? 1 : 2).Execute();
			}
		}

		/// <summary>
		/// Gets the exit code of the most recently terminated console process.
		/// </summary>
		public int LastExitCode
		{
			get
			{
				if(!_isEverRun)
					throw new InvalidOperationException("The console process has never run in this control.");
				return _nLastExitCode;
			}
		}

		/// <summary>
		/// Optional. Overrides the startup directory for the console process.
		/// </summary>
		[CanBeNull]
		public string StartupDirectory { get; set; }

		[Pure]
		public MacroBuilder BeginGuiMacro([NotNull] string sMacroName)
		{
			if(sMacroName == null)
				throw new ArgumentNullException(nameof(sMacroName));

			return new MacroBuilder(this, sMacroName, Enumerable.Empty<string>());
		}

		/// <summary>
		/// The EnumChildWindows function enumerates the child windows that belong to the specified parent window by passing the handle to each child window, in turn, to an application-defined callback function. EnumChildWindows continues until the last child window is enumerated or the callback function returns FALSE.
		/// </summary>
		/// <param name="hWndParent">[in] Handle to the parent window whose child windows are to be enumerated. If this parameter is NULL, this function is equivalent to EnumWindows. Windows 95/98/Me: hWndParent cannot be NULL.</param>
		/// <param name="lpEnumFunc">[in] Pointer to an application-defined callback function. For more information, see EnumChildProc.</param>
		/// <param name="lParam">[in] Specifies an application-defined value to be passed to the callback function.</param>
		/// <returns>Not used.</returns>
		/// <remarks>If a child window has created child windows of its own, EnumChildWindows enumerates those windows as well. A child window that is moved or repositioned in the Z order during the enumeration process will be properly enumerated. The function does not enumerate a child window that is destroyed before being enumerated or that is created during the enumeration process. </remarks>
		[DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
		public static extern Int32 EnumChildWindows(void* hWndParent, void* lpEnumFunc, IntPtr lParam);

		/// <summary>
		/// Gets the startup environment variables. This does not reflect the env vars of a running console process.
		/// </summary>
		[NotNull]
		public IEnumerable<string> EnumEnv()
		{
			return _environment.Keys.ToArray();
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

			string exe = ConEmuConsoleExtenderExecutablePath;
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
						BeginInvoke(FWhenDone, result);
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
		/// Gets the startup environment variables. This does not reflect the env vars of a running console process.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		[CanBeNull]
		public string GetEnv([NotNull] string name)
		{
			if(string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			string value;
			_environment.TryGetValue(name, out value);
			return value;
		}

		public void PasteText([NotNull] string text)
		{
			if(text == null)
				throw new ArgumentNullException(nameof(text));
			if(text.Length == 0)
				return;

			BeginGuiMacro("Paste").WithParam(2).WithParam(text).Execute();
		}

		/// <summary>
		/// Sets the startup environment variables for the console process, before it is started.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public void SetEnv([NotNull] string name, [CanBeNull] string value)
		{
			if(string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			AssertNotRunning();
			if(value == null)
				_environment.Remove(name);
			else
				_environment[name] = value;
		}

		/// <summary>
		/// The SetFocus function sets the keyboard focus to the specified window. The window must be attached to the calling thread's message queue. The SetFocus function sends a WM_KILLFOCUS message to the window that loses the keyboard focus and a WM_SETFOCUS message to the window that receives the keyboard focus. It also activates either the window that receives the focus or the parent of the window that receives the focus. If a window is active but does not have the focus, any key pressed will produce the WM_SYSCHAR, WM_SYSKEYDOWN, or WM_SYSKEYUP message. If the VK_MENU key is also pressed, the lParam parameter of the message will have bit 30 set. Otherwise, the messages produced do not have this bit set. By using the AttachThreadInput function, a thread can attach its input processing to another thread. This allows a thread to call SetFocus to set the keyboard focus to a window attached to another thread's message queue.
		/// </summary>
		/// <param name="hWnd">[in] Handle to the window that will receive the keyboard input. If this parameter is NULL, keystrokes are ignored. </param>
		/// <returns>If the function succeeds, the return value is the handle to the window that previously had the keyboard focus. If the hWnd parameter is invalid or the window is not attached to the calling thread's message queue, the return value is NULL. To get extended error information, call GetLastError.</returns>
		[DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = false, ExactSpelling = true)]
		public static extern void* SetFocus(void* hWnd);

		public void Start()
		{
			if(_process != null)
				throw new InvalidOperationException("Cannot start a new console process because another one is already running.");
			if(string.IsNullOrEmpty(ConsoleCommandLine))
				throw new InvalidOperationException("Cannot start a new console process for command line “{0}” because it's either NULL, or empty, or whitespace.");

			var cmdl = new CommandLineBuilder();

			cmdl.AppendSwitch("-InsideWnd");
			cmdl.AppendFileNameIfNotNull("0x" + Handle.ToString("X"));

			cmdl.AppendSwitch("-LoadCfgFile");
			cmdl.AppendFileNameIfNotNull(EmitConfigFile());

			if(!string.IsNullOrEmpty(StartupDirectory))
			{
				cmdl.AppendSwitch("-Dir");
				cmdl.AppendFileNameIfNotNull(StartupDirectory);
			}

			// This one MUST be the last switch
			// And the shell command line itself
			cmdl.AppendSwitch("-cmd");
			cmdl.AppendSwitch(ConsoleCommandLine);
			if(IsElevated)
				cmdl.AppendSwitchIfNotNull("-cur_console:", "a");

			if(string.IsNullOrEmpty(ConEmuExecutablePath))
			{
				MessageBox.Show("Could not run the console emulator. The path to ConEmu.exe could not be detected.", "Console Emulator", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Start ConEmu
			try
			{
				if(!File.Exists(ConEmuExecutablePath))
					throw new InvalidOperationException($"Missing ConEmu executable at location “{ConEmuExecutablePath}”.");
				var processNew = new Process() {StartInfo = new ProcessStartInfo(ConEmuExecutablePath, cmdl.ToString()) {UseShellExecute = false}};

				// Bind process termination
				processNew.EnableRaisingEvents = true;
				processNew.Exited += delegate
				{
					// Ensure STA
					BeginInvoke(new Action(() =>
					{
						Process processWas = _process;
						if(processWas == null)
							return;
						_nLastExitCode = processWas.ExitCode;
						_process = null;
						Invalidate();
						ConsoleStateChanged?.Invoke(this, EventArgs.Empty);
					}));
				};

				if(!processNew.Start())
					throw new InvalidOperationException("The process did not start.");
				_process = processNew;
				_isEverRun = true;

				ConsoleStateChanged?.Invoke(this, EventArgs.Empty);
			}
			catch(Win32Exception ex)
			{
				MessageBox.Show("Could not run the console emulator. " + ex.Message + $" ({ex.NativeErrorCode:X8})" + Environment.NewLine + Environment.NewLine + "Command:" + Environment.NewLine + ConEmuExecutablePath + Environment.NewLine + Environment.NewLine + "Arguments:" + Environment.NewLine + cmdl, "Console Emulator", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void AssertNotRunning()
		{
			if(_process != null)
				throw new InvalidOperationException("This change is not possible when a console process is already running.");
		}

		public event EventHandler ConsoleStateChanged;

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			// Cleanup console process
			if(_process != null)
			{
				try
				{
					_process.Kill();
				}
				catch(Exception)
				{
					// Nothing to do with it
				}
			}

			// Cleanup working file
			if(_sConEmuSettingsWrittenTempFile != null)
			{
				try
				{
					File.Delete(_sConEmuSettingsWrittenTempFile);
				}
				catch(Exception)
				{
					// Not interested in this exception
				}
			}
		}

		private string EmitConfigFile()
		{
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
				xmlElem.SetAttribute("data", (IsStatusbarVisible ? 1 : 0).ToString());
			}

			// Environment variables
			if(_environment.Any())
			{
				var xmlElem = ((XmlElement)(xmlSettings.SelectSingleNode("value[@name='EnvironmentSet']") ?? xmlSettings.AppendChild(xmldoc.CreateElement("value"))));
				xmlElem.SetAttribute("type", "multi");
				foreach(KeyValuePair<string, string> pair in _environment)
				{
					XmlElement xmlLine;
					xmlElem.AppendChild(xmlLine = xmldoc.CreateElement("line"));
					xmlLine.SetAttribute("data", $"set {pair.Key}={pair.Value}");
				}
			}

			// Write to temp location
			if(_sConEmuSettingsWrittenTempFile == null)
				_sConEmuSettingsWrittenTempFile = Path.GetTempFileName() + ".ConEmuSettings.Xml";
			xmldoc.Save(_sConEmuSettingsWrittenTempFile);

			return _sConEmuSettingsWrittenTempFile;
		}

		[NotNull]
		private static string InitConEmuLocation()
		{
			// Look alongside this DLL and in a subfolder
			string asmpath = new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase, UriKind.Absolute).LocalPath;
			if(string.IsNullOrEmpty(asmpath))
				return "";
			string dir = Path.GetDirectoryName(asmpath);
			if(string.IsNullOrEmpty(dir))
				return "";

			// ConEmu.exe in the same folder as this control DLL
			string candidate = Path.Combine(dir, ConEmuExeName);
			if(File.Exists(candidate))
				return candidate;

			// ConEmu.exe in a subfolder (it's convenient to have all managed product DLLs in the same folder for assembly resolve, and it might be handy to put multifile native deps like ConEmu in a subfolder)
			candidate = Path.Combine(Path.Combine(dir, ConEmuSubfolderName), ConEmuExeName);
			if(File.Exists(candidate))
				return candidate;

			// Not found by our standard means, rely on user to set path, otherwise will fail to start
			return "";
		}

		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);

			void* hwnd = TryGetConEmuHwnd();
			if(hwnd != null)
				SetFocus(hwnd);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			if(IsStartingImmediately)
				Start();

			base.OnHandleCreated(e);
		}

		protected override void OnPaint(PaintEventArgs args)
		{
			if(_process != null)
				return;
			args.Graphics.FillRectangle(SystemBrushes.ControlDark, args.ClipRectangle);
		}

		[NotNull]
		private static string TryDeriveConEmuConsoleExtenderExecutablePath([NotNull] string sConEmuPath)
		{
			if(sConEmuPath == null)
				throw new ArgumentNullException(nameof(sConEmuPath));
			if(sConEmuPath == "")
				return "";
			string dir = Path.GetDirectoryName(sConEmuPath);
			if(string.IsNullOrEmpty(dir))
				return "";

			string candidate = Path.Combine(dir, ConEmuConsoleExtenderExeName);
			if(File.Exists(candidate))
				return candidate;

			candidate = Path.Combine(Path.Combine(dir, ConEmuSubfolderName), "ConEmuC.exe");
			if(File.Exists(candidate))
				return candidate;

			return "";
		}

		[CanBeNull]
		private void* TryGetConEmuHwnd()
		{
			void* hwndConEmu = null;
			EnumWindowsProc callback = (hwnd, param) =>
			{
				*((void**)param) = hwnd;
				return 0;
			};
			EnumChildWindows((void*)Handle, (void*)Marshal.GetFunctionPointerForDelegate(callback), (IntPtr)(&hwndConEmu));
			GC.KeepAlive(callback);
			return hwndConEmu;
		}

		/// <summary>
		/// The EnumWindowsProc function is an application-defined callback function used with the EnumWindows or EnumDesktopWindows function. It receives top-level window handles. The WNDENUMPROC type defines a pointer to this callback function. EnumWindowsProc is a placeholder for the application-defined function name.
		/// </summary>
		/// <param name="hwnd">[in] Handle to a top-level window. </param>
		/// <param name="lParam">[in] Specifies the application-defined value given in EnumWindows or EnumDesktopWindows. </param>
		/// <returns>To continue enumeration, the callback function must return TRUE; to stop enumeration, it must return FALSE.</returns>
		/// <remarks>An application must register this callback function by passing its address to EnumWindows or EnumDesktopWindows. </remarks>
		public delegate Int32 EnumWindowsProc(void* hwnd, IntPtr lParam);

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

		public sealed class MacroBuilder
		{
			[NotNull]
			private readonly ConEmuControl _owner;

			[NotNull]
			private readonly IEnumerable<string> _parameters;

			[NotNull]
			private readonly string _sMacroName;

			internal MacroBuilder([NotNull] ConEmuControl owner, [NotNull] string sMacroName, [NotNull] IEnumerable<string> parameters)
			{
				if(owner == null)
					throw new ArgumentNullException(nameof(owner));
				if(sMacroName == null)
					throw new ArgumentNullException(nameof(sMacroName));
				if(parameters == null)
					throw new ArgumentNullException(nameof(parameters));

				_owner = owner;
				_sMacroName = sMacroName;
				_parameters = parameters;
			}

			/// <summary>
			/// Renders the macro and executes with ConEmu.
			/// </summary>
			/// <param name="FWhenDone">Optional. Executes on the same thread when the macro is done executing.</param>
			public void Execute(Action<GuiMacroResult> FWhenDone = null)
			{
				_owner.ExecuteGuiMacroText(RenderMacroCommand(), FWhenDone);
			}

			/// <summary>
			/// Adds a parameter.
			/// </summary>
			[NotNull]
			[Pure]
			public MacroBuilder WithParam([NotNull] string value)
			{
				if(value == null)
					throw new ArgumentNullException(nameof(value));
				return new MacroBuilder(_owner, _sMacroName, _parameters.Concat(new[] {value}));
			}

			/// <summary>
			/// Adds a parameter.
			/// </summary>
			[NotNull]
			[Pure]
			public MacroBuilder WithParam(int value)
			{
				return WithParam(value.ToString());
			}

			bool isAlphanumeric([NotNull] string s)
			{
				if(s == null)
					throw new ArgumentNullException(nameof(s));
				foreach(char ch in s)
				{
					if((!char.IsLetterOrDigit(ch)) && (ch != '_'))
						return false;
				}
				return true;
			}

			[Pure]
			[NotNull]
			private string RenderMacroCommand()
			{
				var sb = new StringBuilder();
				if(!isAlphanumeric(_sMacroName))
					throw new InvalidOperationException("The macro name must be alphanumeric.");
				sb.Append(_sMacroName);

				foreach(string parameter in _parameters)
				{
					sb.Append(' ');

					if(isAlphanumeric(parameter))
						sb.Append(parameter);
					else
						sb.Append('@').Append('"').Append(parameter.Replace("\"", "\"\"")).Append('"');
				}
				return sb.ToString();
			}
		}
	}

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