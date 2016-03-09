using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using ConEmu.WinForms.Util;

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

		private bool _isEverRun;

		private bool _isStatusbarVisible = true;

		private int _nLastExitCode;

		[CanBeNull]
		private ConEmuSession _running;

		public ConEmuControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.Selectable, true);

			// Prevent downsizing to zero because the current ConEmu implementation asserts on its HWND having positive dimensions
#pragma warning disable once VirtualMemberCallInContructor
			MinimumSize = new Size(1, 1);
		}

		/// <summary>
		///     <para>Gets or sets whether this control will start the console process as soon as it's loaded on the form: yes if non-<c>NULL</c>, and no if <c>NULL</c>.</para>
		///     <para>Set this to <c>NULL</c> to prevent the terminal emulator from opening automatically. Adjust this object or assign a new one to setup the initial terminal emulator.</para>
		///     <para>You can either specify the console executable to run in <see cref="ConEmuStartInfo.ConsoleProcessCommandLine" /> (the console window will close as soon as it exits), or use its default value <see cref="ConEmuConstants.DefaultConsoleCommandLine" /> for the default Windows console and execute your command in that console with <see cref="ConEmuSession.WriteInputText" /> (the console will remain operable after the command completes).</para>
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
		/// Gets or sets whether the status bar should be visible in the terminal (when it's open in the control).
		/// </summary>
		public bool IsStatusbarVisible
		{
			get
			{
				return _isStatusbarVisible;
			}
			set
			{
				_isStatusbarVisible = value;
				_running?.BeginGuiMacro("Status").WithParam(0).WithParam(value ? 1 : 2).ExecuteAsync();
			}
		}

		/// <summary>
		///     <para>Gets the exit code of the most recently terminated console process.</para>
		///     <para><c>NULL</c> if no console process has exited in this control yet.</para>
		///     <para>Note that a console process is currently running in the terminal you'd be getting the previous exit code until it exits.</para>
		/// </summary>
		public int? LastExitCode
		{
			get
			{
				// Special case for just-exited payload: user might get the payload-exited event before us and call this property to get its exit code, while we have not recorded the fresh exit code yet
				// So call into the current session and fetch the actual value, if available (no need to write to field, will update in our event handler soon)
				ConEmuSession running = _running;
				if((running != null) && (running.IsConsoleProcessExited))
					return running.GetConsoleProcessExitCode();
				return _isEverRun ? _nLastExitCode : default(int?); // No terminal open or current process still running in the terminal, use prev exit code if there were
			}
		}

		/// <summary>
		///     <para>Gets the running terminal session, or <c>NULL</c>, if there is currently none.</para>
		///     <para>A session represents an open terminal displayed in the control, in which a console process is either still running, or has already exited.</para>
		///     <para>To get the running session object in a reliable way for possibly short-running sessions, call <see cref="Start" /> explicitly, and pass in the event sinks.</para>
		/// </summary>
		[CanBeNull]
		public ConEmuSession RunningSession => _running;

		/// <summary>
		///     <para>Gets the current state of the control regarding what's running in it.</para>
		///     <para>This only changes on the main thread.</para>
		/// </summary>
		public States TerminalState => _running != null ? (_running.IsConsoleProcessExited ? States.DetachedTerminal : States.TerminalWithConsoleProcess) : (_isEverRun ? States.Exited : States.Empty);

		/// <summary>
		/// Gets whether there is an open terminal displayed in the control. Of <see cref="TerminalState" />, that's either <see cref="States.TerminalWithConsoleProcess" /> or <see cref="States.DetachedTerminal" />.
		/// </summary>
		public bool IsTerminalOpen => _running != null;

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
			TerminalStateChanged?.Invoke(this, EventArgs.Empty);

			session.ConsoleEmulatorClosed += delegate
			{
				try
				{
					_nLastExitCode = _running.GetConsoleProcessExitCode();
				}
				catch(Exception)
				{
					// NOP
				}
				_running = null;
				Invalidate();
				TerminalStateChanged?.Invoke(this, EventArgs.Empty);
			};
			return session;
		}

		[SuppressMessage("ReSharper", "UnusedMember.Local")]
		private void AssertNotRunning()
		{
			if(_running != null)
				throw new InvalidOperationException("This change is not possible when a console process is already running.");
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			// Cleanup console process
			if(_running != null)
			{
				try
				{
					_running.CloseConsoleEmulator();
				}
				catch(Exception)
				{
					// Nothing to do with it
				}
			}
		}

		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);

			void* hwnd = TryGetConEmuHwnd();
			if(hwnd != null)
				WinApi.SetFocus(hwnd);
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			base.OnLayout(levent);
			void* hwnd = TryGetConEmuHwnd();
			if(hwnd != null)
				WinApi.MoveWindow(hwnd, 0, 0, ClientSize.Width, ClientSize.Height, 1);
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

		/// <summary>
		/// Fires on the main thread whenever <see cref="TerminalState" /> changes.
		/// </summary>
		public event EventHandler TerminalStateChanged;

		[CanBeNull]
		private void* TryGetConEmuHwnd()
		{
			void* hwndConEmu = null;
			WinApi.EnumWindowsProc callback = (hwnd, param) =>
			{
				*((void**)param) = hwnd;
				return 0;
			};
			WinApi.EnumChildWindows((void*)Handle, (void*)Marshal.GetFunctionPointerForDelegate(callback), (IntPtr)(&hwndConEmu));
			GC.KeepAlive(callback);
			return hwndConEmu;
		}
	}
}