using System.Runtime.CompilerServices;

using ConEmu.WinForms.Util;

#pragma warning disable CheckNamespace

namespace System.Threading.Tasks
{
	internal struct TaskAwaiter : INotifyCompletion
	{
		private readonly bool _isctx;

		private readonly Task _task;

		internal TaskAwaiter(Task task, bool isctx = true)
		{
			_task = task;
			_isctx = isctx;
		}

		public bool IsCompleted => _task.IsCompleted;

		public void GetResult()
		{
			_task.Wait();
		}

		public void OnCompleted(Action continuation)
		{
			_task.ContinueWith(param0 => continuation(), _isctx ? TaskHelpers.GetTaskSchedulerFromContext() : TaskScheduler.Default);
		}
	}
}