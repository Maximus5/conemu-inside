using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	///     <para>The console emulator control.</para>
	///     <para>If <see cref="AutoStartInfo" /> is non-<c>NULL</c>, immediately starts the emulator on its parameters, and shows the console emulator in the control. Otherwise (or after the process exits), shows the gray background.</para>
	/// </summary>
	public unsafe class ConEmuControl : Control
	{
		private ConEmuStartInfo _autostartinfo = new ConEmuStartInfo(); // Enabled by default, and with all default values

		bool _isEverRun;

		private bool _isStatusbarVisible = true;

		private int _nLastExitCode;

		[CanBeNull]
		ConEmuSession _running;

		public ConEmuControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.Selectable, true);
		}

		/// <summary>
		///     <para>Gets or sets whether this control will start the console process as soon as it's loaded on the form: yes if non-<c>NULL</c>, and no if <c>NULL</c>.</para>
		///     <para>Set this to <c>NULL</c> to prevent the terminal emulator from opening automatically. Adjust this object or assign a new one to setup the initial terminal emulator.</para>
		///     <para>You can either specify the console executable to run in <see cref="ConEmuStartInfo.ConsoleCommandLine" /> (the console window will close as soon as it exits), or use its default value <see cref="ConEmuConstants.DefaultConsoleCommandLine" /> for the default Windows console and execute your command in that console with <see cref="PasteText" /> (the console will remain operable after the command completes).</para>
		/// </summary>
		/// <remarks>
		///     <para>This object cannot be changed after the console emulator starts. The value of the property becomes <c>NULL</c> and cannot be changed either.</para>
		///     <para>If you're chaning <c>NULL</c> to non-<c>NULL</c> and the control has already been loaded, it will start executing with these params immediately, so make sure you've completed settings up all the parameters before making the assignment.</para>
		/// </remarks>
		[CanBeNull]
		public ConEmuStartInfo AutoStartInfo
		{
			get
			{
				return _autostartinfo;
			}
			set
			{
				if(_isEverRun)
					throw new InvalidOperationException("AutoStartInfo can only be changed before the first console process runs in this control.");
				_autostartinfo = value;

				// Invariant: if changed to TRUE past the normal AutoStartInfo checking point
				if((value != null) && (IsHandleCreated))
					Start(value);
			}
		}

		/// <summary>
		/// Gets the state of the console emulator.
		/// </summary>
		public States ConsoleState => _running != null ? States.Running : (_isEverRun ? States.Exited : States.Empty);

		/// <summary>
		/// Whether the console process is currently running.
		/// </summary>
		public bool IsConsoleProcessRunning => _running != null; // TODO: if the process is still running inside conemu

		public bool IsStatusbarVisible
		{
			get
			{
				return _isStatusbarVisible;
			}
			set
			{
				_isStatusbarVisible = value;
				if(_running != null)
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
		/// Starts construction of the ConEmu GUI Macro.
		/// </summary>
		[Pure]
		public GuiMacroBuilder BeginGuiMacro([NotNull] string sMacroName)
		{
			if(sMacroName == null)
				throw new ArgumentNullException(nameof(sMacroName));

			return new GuiMacroBuilder(GetRunningSession(), sMacroName, Enumerable.Empty<string>());
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
		/// Executes a ConEmu GUI Macro on the active console, see http://conemu.github.io/en/GuiMacro.html .
		/// </summary>
		/// <param name="macrotext">The full macro command, see http://conemu.github.io/en/GuiMacro.html .</param>
		/// <param name="FWhenDone">Optional. Executes on the same thread when the macro is done executing.</param>
		public void ExecuteGuiMacroText([NotNull] string macrotext, [CanBeNull] Action<GuiMacroResult> FWhenDone = null)
		{
			GetRunningSession().ExecuteGuiMacroText(macrotext, FWhenDone);
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
		/// The SetFocus function sets the keyboard focus to the specified window. The window must be attached to the calling thread's message queue. The SetFocus function sends a WM_KILLFOCUS message to the window that loses the keyboard focus and a WM_SETFOCUS message to the window that receives the keyboard focus. It also activates either the window that receives the focus or the parent of the window that receives the focus. If a window is active but does not have the focus, any key pressed will produce the WM_SYSCHAR, WM_SYSKEYDOWN, or WM_SYSKEYUP message. If the VK_MENU key is also pressed, the lParam parameter of the message will have bit 30 set. Otherwise, the messages produced do not have this bit set. By using the AttachThreadInput function, a thread can attach its input processing to another thread. This allows a thread to call SetFocus to set the keyboard focus to a window attached to another thread's message queue.
		/// </summary>
		/// <param name="hWnd">[in] Handle to the window that will receive the keyboard input. If this parameter is NULL, keystrokes are ignored. </param>
		/// <returns>If the function succeeds, the return value is the handle to the window that previously had the keyboard focus. If the hWnd parameter is invalid or the window is not attached to the calling thread's message queue, the return value is NULL. To get extended error information, call GetLastError.</returns>
		[DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = false, ExactSpelling = true)]
		public static extern void* SetFocus(void* hWnd);

		/// <summary>
		/// Starts a new console process in the console emulator control.
		/// </summary>
		[NotNull]
		public ConEmuSession Start([NotNull] ConEmuStartInfo startinfo)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));
			if(_running != null)
				throw new InvalidOperationException("Cannot start a new console process because another one is already running.");

			_autostartinfo = null; // As we're starting, no more chance for an autostart
			if(!IsHandleCreated)
				CreateHandle();
			var session = new ConEmuSession(startinfo, new ConEmuSession.HostContext((void*)Handle, IsStatusbarVisible));
			_running = session;
			_isEverRun = true;
			ConsoleStateChanged?.Invoke(this, EventArgs.Empty);

			session.ConsoleEmulatorExited += delegate
			{
				try
				{
					_nLastExitCode = _running.ExitCode;
				}
				catch(Exception)
				{
					// NOP
				}
				_running = null;
				Invalidate();
				ConsoleStateChanged?.Invoke(this, EventArgs.Empty);
			};
			return session;
		}

		[SuppressMessage("ReSharper", "UnusedMember.Local")]
		private void AssertNotRunning()
		{
			if(_running != null)
				throw new InvalidOperationException("This change is not possible when a console process is already running.");
		}

		public event EventHandler ConsoleStateChanged;

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			// Cleanup console process
			if(_running != null)
			{
				try
				{
					_running.Kill();
				}
				catch(Exception)
				{
					// Nothing to do with it
				}
			}
		}

		[NotNull]
		private ConEmuSession GetRunningSession()
		{
			ConEmuSession session = _running;
			if(session == null)
				throw new InvalidOperationException("This operation cannot be executed because the terminal emulator session is not running at the moment.");
			return session;
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
			if(AutoStartInfo != null)
				Start(AutoStartInfo);

			base.OnHandleCreated(e);
		}

		protected override void OnPaint(PaintEventArgs args)
		{
			if(_running != null) // Occupies the whole area
				return;
			args.Graphics.FillRectangle(SystemBrushes.ControlDark, args.ClipRectangle);
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
}