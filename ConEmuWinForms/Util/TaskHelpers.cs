using System.Reflection;
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
	}
}