#pragma warning disable CheckNamespace
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
	public struct AsyncTaskMethodBuilder<TResult>
	{
		private TaskCompletionSource<TResult> _tasker;

		public Task<TResult> Task => _tasker.Task;

		public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
		{
			awaiter.OnCompleted(((IAsyncStateMachine)stateMachine).MoveNext);
		}

		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
		{
			AwaitOnCompleted(ref awaiter, ref stateMachine);
		}

		public static AsyncTaskMethodBuilder<TResult> Create()
		{
			AsyncTaskMethodBuilder<TResult> taskMethodBuilder;
			taskMethodBuilder._tasker = new TaskCompletionSource<TResult>();
			return taskMethodBuilder;
		}

		public void SetException(Exception exception)
		{
			_tasker.SetException(exception);
		}

		public void SetResult(TResult result)
		{
			_tasker.SetResult(result);
		}

		public void SetStateMachine(IAsyncStateMachine stateMachine)
		{
			throw new NotImplementedException();
		}

		public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
		{
			stateMachine.MoveNext();
		}
	}
}