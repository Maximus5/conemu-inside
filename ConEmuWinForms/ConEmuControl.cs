using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

using ConEmu.WinForms.Util;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	///     <para>This is a console emulator control that embeds a fully functional console view in a Windows Forms window. It is capable of running any console application with full interactivity and advanced console functions. Applications will detect it as an actual console and will not fall back to the output redirection mode with reduced interactivity or formatting.</para>
	///     <para>The control can be used to run a console process in the console emulator. The console process is the single command executed in the control, which could be a simple executable (the console emulator is not usable after it exits), or an interactive shell like <c>cmd</c> or <c>powershell</c> or <c>bash</c>, which in turn can execute multiple commands, either by user input or programmatically with <see cref="ConEmuSession.WriteInputText" />. The console emulator is what implements the console and renders the console view in the control. A new console emulator (represented by a <see cref="RunningSession" />) is <see cref="Start">started</see> for each console process. After the root console process terminates, the console emulator might remain open (see <see cref="ConEmuStartInfo.WhenConsoleProcessExits" />) and still present the console window, or get closed. After the console emulator exits, the control is blank until <see cref="Start" /> spawns a new console emulator for a console process in it. You cannot run more than one console emulator (or console process) simultaneousely.</para>
	/// </summary>
	public unsafe class ConEmuControl : Control
	{
		/// <summary>
		/// Enabled by default, and with all default values (runs the cmd shell).
		/// </summary>
		private ConEmuStartInfo _autostartinfo = new ConEmuStartInfo();

		private bool _isStatusbarVisible = true;

		/// <summary>
		/// After the first console process exits (not session), stores its exit code. Changes on the main thread only.
		/// </summary>
		private int? _nLastExitCode;

		/// <summary>
		/// The running session, if currently running.
		/// </summary>
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
		///     <para>Set this to <c>NULL</c> to prevent the console emulator from opening automatically. Adjust this object or assign a new one to setup the console process to be run in the console emulator automatically.</para>
		///     <para>You can either specify the console executable to run in <see cref="ConEmuStartInfo.ConsoleProcessCommandLine" />, or use its default value <see cref="ConEmuConstants.DefaultConsoleCommandLine" /> for the default Windows console and execute your command in that console with <see cref="ConEmuSession.WriteInputText" /> (the console will remain operable and ready to run more commands after the command completes).</para>
		/// </summary>
		/// <remarks>
		///     <para>This object cannot be changed after the first <see cref="RunningSession">console emulator session</see> starts. The value of the property becomes <c>NULL</c> and cannot be changed either.</para>
		///     <para>If you're chaning <c>NULL</c> to non-<c>NULL</c> and the control has already been loaded, it will start executing a new session with these parameters immediately, so make sure you've completed settings up all the parameters before making the assignment.</para>
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
				if(State != States.Unused)
					throw new InvalidOperationException("AutoStartInfo can only be changed before the first console emulator session runs in this control.");
				_autostartinfo = value;

				// Invariant: if changed to TRUE past the normal AutoStartInfo checking point
				if((value != null) && (IsHandleCreated))
					Start(value);
			}
		}

		/// <summary>
		/// Gets or sets whether the status bar of the console emulator view should be visible.
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
		///     <para>Note that if a console process is currently running in the console emulator then you'd be getting the previous exit code until it exits.</para>
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
				return _nLastExitCode; // No console emulator open or current process still running in the console emulator, use prev exit code if there were
			}
		}

		/// <summary>
		///     <para>Gets the running console emulator session, or <c>NULL</c> if there currently is none.</para>
		///     <para>A session represents an open console emulator view displayed in the control, in which a console process is either still running, or has already terminated.</para>
		///     <para>To guarantee getting the running session object of a short-lived session before it closes, call <see cref="Start" /> manually rather than rely on <see cref="AutoStartInfo" />.</para>
		///     <para>This only changes on the main thread.</para>
		/// </summary>
		[CanBeNull]
		public ConEmuSession RunningSession => _running;

		/// <summary>
		///     <para>Gets the current state of the console emulator control regarding whether a console emulator is open in it, and whether there is still a console process running in that emulator.</para>
		///     <para>This only changes on the main thread.</para>
		/// </summary>
		public States State => _running != null ? (_running.IsConsoleProcessExited ? States.ConsoleEmulatorEmpty : States.ConsoleEmulatorWithConsoleProcess) : (_nLastExitCode.HasValue ? States.Recycled : States.Unused);

		/// <summary>
		///     <para>Gets whether a console emulator is currently open, and its console window view is displayed in the control. Of <see cref="State" />, that's either <see cref="States.ConsoleEmulatorWithConsoleProcess" /> or <see cref="States.ConsoleEmulatorEmpty" />.</para>
		///     <para>When a console emulator is not open, the control is blank.</para>
		///     <para>This only changes on the main thread.</para>
		/// </summary>
		public bool IsConsoleEmulatorOpen => _running != null;

		/// <summary>
		///     <para>Starts a new console process in the console emulator control, and shows the console emulator view. When a session is not running, the control is blank.</para>
		///     <para>If another <see cref="RunningSession">session</see> is running, it will be closed, and the new session will replace it. <see cref="AutoStartInfo" /> will call <see cref="Start" /> and create a session automatically if configured.</para>
		/// </summary>
		/// <remarks>
		///     <para>The control state transitions to <see cref="States.ConsoleEmulatorWithConsoleProcess" />, then to <see cref="States.ConsoleEmulatorEmpty" /> (unless configured to close on exit), then to <see cref="States.Recycled" />.</para>
		/// </remarks>
		/// <returns>Returns the newly-started session, as in <see cref="RunningSession" />.</returns>
		[NotNull]
		public ConEmuSession Start([NotNull] ConEmuStartInfo startinfo)
		{
			if(startinfo == null)
				throw new ArgumentNullException(nameof(startinfo));

			// Close prev session if there is one
			_running?.CloseConsoleEmulator();
			if(_running != null)
				throw new InvalidOperationException("Cannot start a new console process because another console emulator session has failed to close in due time.");

			_autostartinfo = null; // As we're starting, no more chance for an autostart
			if(!IsHandleCreated)
				CreateHandle();

			// Spawn session
			var session = new ConEmuSession(startinfo, new ConEmuSession.HostContext((void*)Handle, IsStatusbarVisible));
			_running = session;
			StateChanged?.Invoke(this, EventArgs.Empty);
 	 	 	startinfo.ConsoleProcessPreExitedEventSink += (sender, args) => OnSessionEnds();
			// Wait for its exit
			session.WaitForConsoleEmulatorCloseAsync().ContinueWith(scheduler : TaskScheduler.FromCurrentSynchronizationContext(), continuationAction : task =>
			{
				OnSessionEnds();
			});

			return session;
		}

 	 	private void OnSessionEnds()
 	 	{
 	 	 	try
 	 	 	{
 	 	 	 	_nLastExitCode = _running.GetConsoleProcessExitCode();
 	 	 	}
 	 	 	catch (Exception)
 	 	 	{
 	 	 	 	// NOP
 	 	 	}
 	 	 	_running = null;
 	 	 	Invalidate();
 	 	 	StateChanged?.Invoke(this, EventArgs.Empty);
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
		/// Fires on the main thread whenever <see cref="State" /> changes.
		/// </summary>
		public event EventHandler StateChanged;

		[CanBeNull]
		private void* TryGetConEmuHwnd()
		{
			if(!IsHandleCreated) // Without this check, getting the Handle would cause the control to be loaded, and AutoStartInfo be executed right in the .ctor, because the first call into this func goes in the .ctor
				return null;
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