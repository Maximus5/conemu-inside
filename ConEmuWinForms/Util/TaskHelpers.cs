using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace ConEmu.WinForms.Util
{
	internal static class TaskHelpers
	{
		private static Task _completedTask;

		/// <summary>Gets a task that's already been completed successfully.</summary>
		[NotNull]
		public static Task CompletedTask
		{
			get
			{
				Task task = _completedTask;
				if(task == null)
				{
					var src = new TaskCompletionSource<Missing>();
					src.SetResult(Missing.Value);
					_completedTask = task = src.Task;
				}
				return task;
			}
		}

		/// <summary>
		/// Creates a started tasks which will get a completed state after the specified amount of time.
		/// </summary>
		[NotNull]
		public static Task Delay(TimeSpan delay)
		{
			if(delay.Ticks < 0L)
				throw new ArgumentOutOfRangeException(nameof(delay), "The timeout must be non-negative.");
			var tasker = new TaskCompletionSource<bool>();
			Timer timer = null;
			timer = new Timer(o =>
			{
				timer.Dispose();
				tasker.TrySetResult(true);
			}, Missing.Value, -1, -1);
			timer.Change(delay, TimeSpan.FromMilliseconds(-1.0));
			return tasker.Task;
		}

		/// <summary>Gets a task that's already been completed successfully.</summary>
		[NotNull]
		public static Task<TResult> FromResult<TResult>(TResult result)
		{
			var completionSource = new TaskCompletionSource<TResult>(result);
			completionSource.TrySetResult(result);
			return completionSource.Task;
		}

		public static TaskAwaiter GetAwaiter(this Task task)
		{
			return new TaskAwaiter(task, true);
		}

		public static TaskAwaiter<T> GetAwaiter<T>(this Task<T> task)
		{
			return new TaskAwaiter<T>(task, true);
		}

		public static TaskAwaiter GetAwaiter(this Task<Task> task)
		{
			return task.Unwrap().GetAwaiter();
		}

		public static TaskAwaiter<T> GetAwaiter<T>(this Task<Task<T>> task)
		{
			return task.Unwrap().GetAwaiter();
		}

		internal static TaskScheduler GetTaskSchedulerFromContext()
		{
			return SynchronizationContext.Current == null ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
		}
	}
}