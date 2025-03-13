namespace ZeroGdk.Server
{
	public readonly struct AsyncScope(SynchronizationContext? context) : IDisposable
	{
		private readonly SynchronizationContext? _context = context;

		public void Dispose()
		{
			SynchronizationContext.SetSynchronizationContext(_context);
		}
	}
}
