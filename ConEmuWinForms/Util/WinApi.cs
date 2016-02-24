using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.Win32.SafeHandles;

namespace ConEmu.WinForms.Util
{
	[SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
	internal static unsafe class WinApi
	{
		public static readonly uint SYNCHRONIZE = 1048576;

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

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern bool FreeLibrary(void* hModule);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
		public static extern void* GetProcAddress(void* hModule, string lpProcName);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern void* LoadLibrary(string libname);

		/// <summary>Opens an existing local process object.</summary>
		/// <param name="dwDesiredAccess">The access to the process object.</param>
		/// <param name="bInheritHandle">If this value is TRUE, processes created by this process will inherit the handle.</param>
		/// <param name="dwProcessId">The identifier of the local process to be opened. If the specified process is the System Process (0x00000000), the function fails and the last error code is ERROR_INVALID_PARAMETER. If the specified process is the Idle process or one of the CSRSS processes, this function fails and the last error code is ERROR_ACCESS_DENIED because their access restrictions prevent user-level code from opening them.</param>
		/// <returns>If the function succeeds, the return value is an open handle to the specified process. If the function fails, the return value is NULL. To get extended error information, call GetLastError.</returns>
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true, ExactSpelling = true)]
		public static extern void* OpenProcess(UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);

		/// <summary>
		/// The SetFocus function sets the keyboard focus to the specified window. The window must be attached to the calling thread's message queue. The SetFocus function sends a WM_KILLFOCUS message to the window that loses the keyboard focus and a WM_SETFOCUS message to the window that receives the keyboard focus. It also activates either the window that receives the focus or the parent of the window that receives the focus. If a window is active but does not have the focus, any key pressed will produce the WM_SYSCHAR, WM_SYSKEYDOWN, or WM_SYSKEYUP message. If the VK_MENU key is also pressed, the lParam parameter of the message will have bit 30 set. Otherwise, the messages produced do not have this bit set. By using the AttachThreadInput function, a thread can attach its input processing to another thread. This allows a thread to call SetFocus to set the keyboard focus to a window attached to another thread's message queue.
		/// </summary>
		/// <param name="hWnd">[in] Handle to the window that will receive the keyboard input. If this parameter is NULL, keystrokes are ignored. </param>
		/// <returns>If the function succeeds, the return value is the handle to the window that previously had the keyboard focus. If the hWnd parameter is invalid or the window is not attached to the calling thread's message queue, the return value is NULL. To get extended error information, call GetLastError.</returns>
		[DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = false, ExactSpelling = true)]
		public static extern void* SetFocus(void* hWnd);

		/// <summary>
		/// The EnumWindowsProc function is an application-defined callback function used with the EnumWindows or EnumDesktopWindows function. It receives top-level window handles. The WNDENUMPROC type defines a pointer to this callback function. EnumWindowsProc is a placeholder for the application-defined function name.
		/// </summary>
		/// <param name="hwnd">[in] Handle to a top-level window. </param>
		/// <param name="lParam">[in] Specifies the application-defined value given in EnumWindows or EnumDesktopWindows. </param>
		/// <returns>To continue enumeration, the callback function must return TRUE; to stop enumeration, it must return FALSE.</returns>
		/// <remarks>An application must register this callback function by passing its address to EnumWindows or EnumDesktopWindows. </remarks>
		public delegate Int32 EnumWindowsProc(void* hwnd, IntPtr lParam);

		public static class Helpers
		{
			[NotNull]
			public static Task<bool> WaitForProcessExitAsync(uint pid)
			{
				void* hProcess = OpenProcess(SYNCHRONIZE, 0, pid);
				if(hProcess == null)
					return TaskHelpers.FromResult(false);

				var tasker = new TaskCompletionSource<bool>();

				var hProcessSafe = new NativeWaitHandle(hProcess, true);
				RegisteredWaitHandle registered = null;
				WaitOrTimerCallback λHappened = (state, timeout) =>
				{
					hProcessSafe.Close();
#pragma warning disable once AccessToModifiedClosure
					registered?.Unregister(null);
					tasker.SetResult(true);
				};
				registered = ThreadPool.RegisterWaitForSingleObject(hProcessSafe, λHappened, Missing.Value, TimeSpan.MaxValue, true);

				return tasker.Task;
			}
		}

		public class NativeWaitHandle : WaitHandle
		{
			public NativeWaitHandle(void* handle, bool isOwning)
			{
				SafeWaitHandle = new SafeWaitHandle((IntPtr)handle, isOwning);
			}
		}
	}
}