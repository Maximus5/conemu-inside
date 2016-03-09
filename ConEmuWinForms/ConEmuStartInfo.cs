using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	/// Describes the parameters for running the console process in the console emulator, including the command line to run.
	/// </summary>
	public sealed class ConEmuStartInfo
	{
		[NotNull]
		private readonly IDictionary<string, string> _environment = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

		[CanBeNull]
		private EventHandler<AnsiStreamChunkEventArgs> _evtAnsiStreamChunkReceivedEventSink;

		[CanBeNull]
		private EventHandler _evtConsoleEmulatorClosedEventSink;

		[CanBeNull]
		private EventHandler<ConsoleProcessExitedEventArgs> _evtConsoleProcessExitedEventSink;

		private bool _isEchoingConsoleCommandLine;

		private bool _isElevated;

		private bool _isReadingAnsiStream;

		private bool _isUsedUp;

		[NotNull]
		private string _sConEmuConsoleExtenderExecutablePath = "";

		[NotNull]
		private string _sConEmuConsoleServerExecutablePath = "";

		[NotNull]
		private string _sConEmuExecutablePath = "";

		[NotNull]
		private string _sConsoleProcessCommandLine = ConEmuConstants.DefaultConsoleCommandLine;

		[NotNull]
		private string _sGreetingText = "";

		[CanBeNull]
		private string _sStartupDirectory;

		private WhenConsoleProcessExits _whenConsoleProcessExits = WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage;

		/// <summary>
		///     <para>Creates a new object with all the parameters in their default values.</para>
		///     <para>The console emulator will run the default <c>CMD</c> shell with such an empty object.</para>
		/// </summary>
		public ConEmuStartInfo()
		{
			ConEmuExecutablePath = InitConEmuLocation();
		}

		/// <summary>
		/// Creates a new object and defines the command line for the console process to be run in the console emulator, <see cref="ConsoleProcessCommandLine" />.
		/// </summary>
		/// <param name="sConsoleProcessCommandLine">Value for <see cref="ConsoleProcessCommandLine" />.</param>
		public ConEmuStartInfo([NotNull] string sConsoleProcessCommandLine)
			: this()
		{
			if(sConsoleProcessCommandLine == null)
				throw new ArgumentNullException(nameof(sConsoleProcessCommandLine));
		}

		/// <summary>
		///     <para>Gets or sets an event sink for the <see cref="ConEmuSession.AnsiStreamChunkReceived" /> event even before the console process starts, which guarantees that your event sink won't miss the early data written by the console process on its very startup.</para>
		///     <para>Settings this to a non-<c>NULL</c> value also implies on <see cref="IsReadingAnsiStream" />.</para>
		/// </summary>
		[CanBeNull]
		public EventHandler<AnsiStreamChunkEventArgs> AnsiStreamChunkReceivedEventSink
		{
			get
			{
				return _evtAnsiStreamChunkReceivedEventSink;
			}
			set
			{
				AssertNotUsedUp();
				_evtAnsiStreamChunkReceivedEventSink = value;
			}
		}

		/// <summary>
		///     <para>Advanced configuration.</para>
		///     <para>Gets or sets the path to the ConEmu console extender (<c>ConEmuC.exe</c>).</para>
		///     <para>Normally, will be autodetected from the path to this DLL or from <see cref="ConEmuExecutablePath" />.</para>
		/// </summary>
		[NotNull]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
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
		///     <para>Advanced configuration.</para>
		///     <para>Gets or sets the path to the ConEmu console server (<c>ConEmuCD.dll</c>). MUST match the processor architecture of the current process.</para>
		///     <para>Normally, will be autodetected from the path to this DLL or from <see cref="ConEmuExecutablePath" />.</para>
		/// </summary>
		[NotNull]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public string ConEmuConsoleServerExecutablePath
		{
			get
			{
				return _sConEmuConsoleServerExecutablePath;
			}
			set
			{
				if(value == null)
					throw new ArgumentNullException(nameof(value));
				if((value == "") && (_sConEmuConsoleServerExecutablePath == ""))
					return;
				if(value == "")
					throw new ArgumentOutOfRangeException(nameof(value), value, "Cannot reset path to an empty string.");
				_sConEmuConsoleServerExecutablePath = value; // Delay existence check 'til we call it
			}
		}

		/// <summary>
		///     <para>Advanced configuration.</para>
		///     <para>Gets or sets the path to the <c>ConEmu.exe</c> which will be the console emulator root process.</para>
		///     <para>Normally, will be autodetected from the path to this DLL.</para>
		/// </summary>
		[NotNull]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
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
				if(_sConEmuConsoleServerExecutablePath == "")
					_sConEmuConsoleServerExecutablePath = TryDeriveConEmuConsoleServerExecutablePath(_sConEmuExecutablePath);
			}
		}

		/// <summary>
		///     <para>Gets or sets an event sink for <see cref="ConEmuSession.ConsoleEmulatorClosed" /> even before the console process starts, which guarantees that your event sink won't miss events even for short-lived processes.</para>
		///     <para>Alternatively, use <see cref="ConEmuSession.WaitForConsoleEmulatorCloseAsync" /> for reliable notification.</para>
		/// </summary>
		[CanBeNull]
		public EventHandler ConsoleEmulatorClosedEventSink
		{
			get
			{
				return _evtConsoleEmulatorClosedEventSink;
			}
			set
			{
				AssertNotUsedUp();
				_evtConsoleEmulatorClosedEventSink = value;
			}
		}

		/// <summary>
		///     <para>The command line to execute in the console emulator as the top-level console process. Each console emulator session can run only one console command.</para>
		///     <para>The default is <see cref="ConEmuConstants.DefaultConsoleCommandLine" />, which opens an interactive <c>CMD</c> shell in the console emulator.</para>
		///     <para>This property cannot be changed when the process is running.</para>
		/// </summary>
		[NotNull]
		public string ConsoleProcessCommandLine
		{
			get
			{
				return _sConsoleProcessCommandLine;
			}
			set
			{
				AssertNotUsedUp();
				_sConsoleProcessCommandLine = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets an event sink for <see cref="ConEmuSession.ConsoleProcessExited" /> even before the console process starts, which guarantees that your event sink won't miss events even for short-lived processes.</para>
		///     <para>Alternatively, use <see cref="ConEmuSession.WaitForConsoleProcessExitAsync" /> for reliable notification.</para>
		/// </summary>
		[CanBeNull]
		public EventHandler<ConsoleProcessExitedEventArgs> ConsoleProcessExitedEventSink
		{
			get
			{
				return _evtConsoleProcessExitedEventSink;
			}
			set
			{
				AssertNotUsedUp();
				_evtConsoleProcessExitedEventSink = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets the custom greeting text which will be echoed into the console emulator stdout before the <see cref="ConsoleProcessCommandLine" /> starts executing.</para>
		///     <para>Note that to echo the <see cref="ConsoleProcessCommandLine" /> itself you can use the more specific <see cref="IsEchoingConsoleCommandLine" /> option (which prints after the custom greeting text).</para>
		///     <para>Newline handling: a newline is added automatically at the end, if missing; if there's a single newline at the end, it is retained AS IS. If you want an empty line after text, add a double newline.</para>
		///     <para>The default is an empty string for no custom greeting.</para>
		///     <para>This property cannot be changed when the process is running.</para>
		/// </summary>
		[NotNull]
		public string GreetingText
		{
			get
			{
				return _sGreetingText;
			}
			set
			{
				if(value == null)
					throw new ArgumentNullException(nameof(value));
				AssertNotUsedUp();
				_sGreetingText = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets whether the <see cref="ConsoleProcessCommandLine">console command line</see> will be echoed into the console emulator stdout before being executed. If there's also <see cref="GreetingText" />, it goes first.</para>
		///     <para>The default is <c>False</c>.</para>
		///     <para>This property cannot be changed when the process is running.</para>
		/// </summary>
		public bool IsEchoingConsoleCommandLine
		{
			get
			{
				return _isEchoingConsoleCommandLine;
			}
			set
			{
				AssertNotUsedUp();
				_isEchoingConsoleCommandLine = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets whether the console process is to be run elevated (an elevation prompt will be shown as needed).</para>
		///     <para>The default is <c>False</c>.</para>
		///     <para>This property cannot be changed when the process is running.</para>
		/// </summary>
		public bool IsElevated
		{
			get
			{
				return _isElevated;
			}
			set
			{
				AssertNotUsedUp();
				_isElevated = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets whether the console emulator will be reading the raw ANSI stream of the console and firing the <see cref="ConEmuSession.AnsiStreamChunkReceived" /> events (and notifying <see cref="AnsiStreamChunkReceivedEventSink" />).</para>
		///     <para>This can only be decided on before the console process starts.</para>
		///     <para>Setting <see cref="AnsiStreamChunkReceivedEventSink" /> to a non-<c>NULL</c> value implies on a <c>True</c> value for this property.</para>
		/// </summary>
		public bool IsReadingAnsiStream
		{
			get
			{
				return _isReadingAnsiStream || (_evtAnsiStreamChunkReceivedEventSink != null);
			}
			set
			{
				AssertNotUsedUp();
				if((!value) && (_evtAnsiStreamChunkReceivedEventSink != null))
					throw new ArgumentOutOfRangeException(nameof(value), false, "Cannot turn IsReadingAnsiStream off when AnsiStreamChunkReceivedEventSink has a non-NULL value because it implies on a True value for this property.");
				_isReadingAnsiStream = value;
			}
		}

		/// <summary>
		/// Optional. Overrides the startup directory for the console process.
		/// </summary>
		[CanBeNull]
		public string StartupDirectory
		{
			get
			{
				return _sStartupDirectory;
			}
			set
			{
				AssertNotUsedUp();
				_sStartupDirectory = value;
			}
		}

		/// <summary>
		///     <para>Gets or sets whether the console emulator view should remain open and keep displaying the last console contents after the console process specified in <see cref="ConsoleProcessCommandLine" /> terminates.</para>
		///     <para>See comments on enum members for details on specific behavior.</para>
		///     <para>The default is <see cref="WinForms.WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage" />.</para>
		///     <para>This property cannot be changed when the process is running.</para>
		/// </summary>
		public WhenConsoleProcessExits WhenConsoleProcessExits
		{
			get
			{
				return _whenConsoleProcessExits;
			}
			set
			{
				AssertNotUsedUp();
				_whenConsoleProcessExits = value;
			}
		}

		/// <summary>
		/// Gets the startup environment variables. This does not reflect the env vars of a running console process.
		/// </summary>
		[NotNull]
		public IEnumerable<string> EnumEnv()
		{
			return _environment.Keys.ToArray();
		}

		/// <summary>
		/// Gets the startup environment variable. This does not reflect the env vars of a running console process.
		/// </summary>
		/// <param name="name">Environment variable name, case-insensitive.</param>
		/// <returns>Environment variable value.</returns>
		[CanBeNull]
		public string GetEnv([NotNull] string name)
		{
			if(string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			string value;
			_environment.TryGetValue(name, out value);
			return value;
		}

		/// <summary>
		///     <para>Sets the startup environment variable for the console process, before it is started.</para>
		///     <para>This cannot be used to change the environment variables of a running console process.</para>
		/// </summary>
		/// <param name="name">Environment variable name, case-insensitive.</param>
		/// <param name="value">Environment variable value, or <c>NULL</c> to remove this environment variable.</param>
		public void SetEnv([NotNull] string name, [CanBeNull] string value)
		{
			if(string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			AssertNotUsedUp();
			if(value == null)
				_environment.Remove(name);
			else
				_environment[name] = value;
		}

		private void AssertNotUsedUp()
		{
			if(_isUsedUp)
				throw new InvalidOperationException("This change is not possible because the start info object has already been used up.");
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
			string candidate = Path.Combine(dir, ConEmuConstants.ConEmuExeName);
			if(File.Exists(candidate))
				return candidate;

			// ConEmu.exe in a subfolder (it's convenient to have all managed product DLLs in the same folder for assembly resolve, and it might be handy to put multifile native deps like ConEmu in a subfolder)
			candidate = Path.Combine(Path.Combine(dir, ConEmuConstants.ConEmuSubfolderName), ConEmuConstants.ConEmuExeName);
			if(File.Exists(candidate))
				return candidate;

			// Not found by our standard means, rely on user to set path, otherwise will fail to start
			return "";
		}

		/// <summary>
		/// Marks this instance as used-up and prevens further modifications.
		/// </summary>
		internal void MarkAsUsedUp()
		{
			_isUsedUp = true;
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

			string candidate = Path.Combine(dir, ConEmuConstants.ConEmuConsoleExtenderExeName);
			if(File.Exists(candidate))
				return candidate;

			candidate = Path.Combine(Path.Combine(dir, ConEmuConstants.ConEmuSubfolderName), ConEmuConstants.ConEmuConsoleExtenderExeName);
			if(File.Exists(candidate))
				return candidate;

			return "";
		}

		[NotNull]
		private static string TryDeriveConEmuConsoleServerExecutablePath([NotNull] string sConEmuPath)
		{
			if(sConEmuPath == null)
				throw new ArgumentNullException(nameof(sConEmuPath));
			if(sConEmuPath == "")
				return "";
			string dir = Path.GetDirectoryName(sConEmuPath);
			if(string.IsNullOrEmpty(dir))
				return "";

			// Make up the file name for the CPU arch of the current process, as we're gonna load it in-process
			string sFileName = ConEmuConstants.ConEmuConsoleServerFileNameNoExt;
			if(IntPtr.Size == 8)
				sFileName += "64";
			sFileName += ".dll";

			string candidate = Path.Combine(dir, sFileName);
			if(File.Exists(candidate))
				return candidate;

			candidate = Path.Combine(Path.Combine(dir, ConEmuConstants.ConEmuSubfolderName), sFileName);
			if(File.Exists(candidate))
				return candidate;

			return "";
		}
	}
}