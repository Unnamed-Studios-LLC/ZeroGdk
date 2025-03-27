namespace ZeroGdk.Server.Timing
{
	internal interface ITicker
	{
		int Delta { get; }
		long Total { get; }

		void Start();
		void Stop();
		void WaitNext();
	}
}
