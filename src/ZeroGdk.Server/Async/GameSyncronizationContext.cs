using System.Diagnostics;

namespace ZeroGdk.Server
{
	internal sealed class GameSynchronizationContext : SynchronizationContext
	{
		private struct ActionRequest(SendOrPostCallback callback, object? state, ManualResetEvent? waitEvent = null)
		{
			private readonly SendOrPostCallback _callback = callback;
			private readonly object? _state = state;
			private readonly ManualResetEvent? _waitEvent = waitEvent;

			public void Invoke()
			{
				try
				{
					_callback(_state);
				}
				finally
				{
					_waitEvent?.Set();
				}
			}
		}

		private const int InitialCapacity = 20;

		private readonly List<ActionRequest> _pendingActions;
		private readonly List<ActionRequest> _actionsToExecute = new(InitialCapacity);
		private readonly int _mainThreadId;
		private int _count = 0;

		private GameSynchronizationContext(int mainThreadId)
		{
			_pendingActions = new List<ActionRequest>(InitialCapacity);
			_mainThreadId = mainThreadId;
		}

		private GameSynchronizationContext(List<ActionRequest> pendingActions, int mainThreadId)
		{
			_pendingActions = pendingActions;
			_mainThreadId = mainThreadId;
		}

		public static bool IsMainThread => Environment.CurrentManagedThreadId == Instance?._mainThreadId;
		private static GameSynchronizationContext? Instance { get; set; }

		public static bool Close(int msTimeout)
		{
			var clearedTasks = WaitForPending(msTimeout);
			SetSynchronizationContext(null);
			return clearedTasks;
		}

		public static AsyncScope CreateScope()
		{
			var current = Current;
			if (Instance == null)
			{
				throw new InvalidOperationException("GameSynchronizationContext has not been initialized!");
			}
			SetSynchronizationContext(Instance);
			return new AsyncScope(current);
		}

		public static void InitializeOnCurrentThread()
		{
			if (Instance != null)
			{
				throw new InvalidOperationException("GameSynchronizationContext has already been initialized!");
			}
			Instance = new GameSynchronizationContext(Environment.CurrentManagedThreadId);
			SetSynchronizationContext(Instance);
		}

		public static void Run()
		{
			if (Current is not GameSynchronizationContext context)
			{
				return;
			}
			context.Execute();
		}

		public override SynchronizationContext CreateCopy()
		{
			return new GameSynchronizationContext(_pendingActions, _mainThreadId);
		}

		public override void OperationStarted()
		{
			Interlocked.Increment(ref _count);
		}
		public override void OperationCompleted()
		{
			Interlocked.Decrement(ref _count);
		}

		public override void Post(SendOrPostCallback callback, object? state)
		{
			lock (_pendingActions)
			{
				_pendingActions.Add(new ActionRequest(callback, state));
			}
		}

		public override void Send(SendOrPostCallback callback, object? state)
		{
			if (_mainThreadId == Thread.CurrentThread.ManagedThreadId)
			{
				callback(state);
			}
			else
			{
				using var waitHandle = new ManualResetEvent(false);
				lock (_pendingActions)
				{
					_pendingActions.Add(new ActionRequest(callback, state, waitHandle));
				}
				waitHandle.WaitOne();
			}
		}

		private static bool WaitForPending(long msTimeout)
		{
			if (Current is not GameSynchronizationContext context)
			{
				return true;
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			while (context.HasPendingTasks())
			{
				if (stopwatch.ElapsedMilliseconds > msTimeout)
				{
					break;
				}

				context.Execute();
				Thread.Sleep(1);
			}

			return !context.HasPendingTasks();
		}

		private void Execute()
		{
			lock (_pendingActions)
			{
				_actionsToExecute.AddRange(_pendingActions);
				_pendingActions.Clear();
			}

			while (_actionsToExecute.Count > 0)
			{
				var action = _actionsToExecute[0];
				_actionsToExecute.RemoveAt(0);
				action.Invoke();
			}
		}

		private bool HasPendingTasks()
		{
			return _pendingActions.Count != 0 ||
				_count != 0;
		}
	}
}
