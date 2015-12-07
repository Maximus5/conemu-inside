using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace ConEmu.WinForms
{
	public unsafe class ConEmuControl : Control
	{
		[NotNull]
		static readonly string ConEmuExeName = "conemu.exe";

		[NotNull]
		private static readonly string ConEmuSubfolderName = "ConEmu";

		private Process _process;

		[CanBeNull]
		private string _sConEmuSettingsFile;

		private States _state;

		public ConEmuControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.Selectable, true);
			ConEmuExecutablePath = InitConEmuLocation();
		}

		public string ConEmuExecutablePath { get; set; }

		[NotNull]
		public string ConsoleCommandLine { get; set; } = "%COMSPEC%";

		public States ConsoleState
		{
			get
			{
				return _state;
			}
		}

		public bool IsElevated { get; set; }

		/// <summary>
		///     <para>Gets or sets whether this control will start the console process as soon as it's loaded on the form.</para>
		///     <para>You can either specify the console executable to run in <see cref="ConsoleCommandLine" /> (the console window will close as soon as it exits), or use its default value for <c>COMSPEC</c> and execute your command in that console with <see cref="PasteText" /> (the console will remain operable after the command completes).</para>
		/// </summary>
		public bool IsStartingImmediately { get; set; } = true;

		/// <summary>
		/// Optional. Overrides the startup directory for the console process.
		/// </summary>
		[CanBeNull]
		public string StartupDirectory { get; set; }

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
		/// The SetFocus function sets the keyboard focus to the specified window. The window must be attached to the calling thread's message queue. The SetFocus function sends a WM_KILLFOCUS message to the window that loses the keyboard focus and a WM_SETFOCUS message to the window that receives the keyboard focus. It also activates either the window that receives the focus or the parent of the window that receives the focus. If a window is active but does not have the focus, any key pressed will produce the WM_SYSCHAR, WM_SYSKEYDOWN, or WM_SYSKEYUP message. If the VK_MENU key is also pressed, the lParam parameter of the message will have bit 30 set. Otherwise, the messages produced do not have this bit set. By using the AttachThreadInput function, a thread can attach its input processing to another thread. This allows a thread to call SetFocus to set the keyboard focus to a window attached to another thread's message queue.
		/// </summary>
		/// <param name="hWnd">[in] Handle to the window that will receive the keyboard input. If this parameter is NULL, keystrokes are ignored. </param>
		/// <returns>If the function succeeds, the return value is the handle to the window that previously had the keyboard focus. If the hWnd parameter is invalid or the window is not attached to the calling thread's message queue, the return value is NULL. To get extended error information, call GetLastError.</returns>
		[DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = false, ExactSpelling = true)]
		public static extern void* SetFocus(void* hWnd);

		public void Start()
		{
			if(_state != States.Empty)
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
				_process = Process.Start(ConEmuExecutablePath, cmdl.ToString());
			}
			catch(Win32Exception ex)
			{
				MessageBox.Show("Could not run the console emulator. " + ex.Message + $" ({ex.NativeErrorCode:X8})" + Environment.NewLine + Environment.NewLine + "Command:" + Environment.NewLine + ConEmuExecutablePath + Environment.NewLine + Environment.NewLine + "Arguments:" + Environment.NewLine + cmdl, "Console Emulator", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			// Cleanup working file
			if(_sConEmuSettingsFile != null)
			{
				try
				{
					File.Delete(_sConEmuSettingsFile);
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

			// Apply settings from properties
			// …

			// Write to temp location
			if(_sConEmuSettingsFile == null)
				_sConEmuSettingsFile = Path.GetTempFileName() + ".ConEmuSettings.Xml";
			xmldoc.Save(_sConEmuSettingsFile);

			return _sConEmuSettingsFile;
		}

		[CanBeNull]
		private static string InitConEmuLocation()
		{
			// Look alongside this DLL and in a subfolder
			string asmpath = new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase, UriKind.Absolute).LocalPath;
			if(string.IsNullOrEmpty(asmpath))
				return null;
			string dir = Path.GetDirectoryName(asmpath);
			if(string.IsNullOrEmpty(dir))
				return null;

			// ConEmu.exe in the same folder as this control DLL
			string candidate = Path.Combine(dir, ConEmuExeName);
			if(File.Exists(candidate))
				return candidate;

			// ConEmu.exe in a subfolder (it's convenient to have all managed product DLLs in the same folder for assembly resolve, and it might be handy to put multifile native deps like ConEmu in a subfolder)
			candidate = Path.Combine(Path.Combine(dir, ConEmuSubfolderName), ConEmuExeName);
			if(File.Exists(candidate))
				return candidate;

			// Not found by our standard means, rely on user to set path, otherwise will fail to start
			return null;
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

		protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
		{
			Trace.WriteLine($"PvKD {e.KeyData}");
			base.OnPreviewKeyDown(e);
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			Trace.WriteLine($"PCK {keyData}");
			return base.ProcessCmdKey(ref msg, keyData);
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			Trace.WriteLine($"PDK: {keyData}");
			return base.ProcessDialogKey(keyData);
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
	}

	public enum States
	{
		Empty,

		Running
	}
}