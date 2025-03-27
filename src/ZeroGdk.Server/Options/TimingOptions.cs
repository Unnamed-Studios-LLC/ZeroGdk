namespace ZeroGdk.Server
{
	public class TimingOptions
	{
		/// <summary>
		/// The maximum number of update steps to process in a single tick when catching up to real-time.
		/// </summary>
		public int MaxDeltaBatches { get; set; } = 1;
		/// <summary>
		/// Determines how the server handles timing during lag.
		/// Use <see cref="TimingStrategy.Realtime"/> to stay close to real-time by speeding up after a delay.
		/// Use <see cref="TimingStrategy.FixedTick"/> to allow the server to fall behind real-time and process ticks at a normal pace once lag subsides.
		/// </summary>
		public TimingStrategy Strategy { get; set; } = TimingStrategy.FixedTick;
		/// <summary>
		/// The target time interval (in milliseconds) between updates under normal conditions.
		/// </summary>
		public int UpdateIntervalMs { get; set; } = 50;
		/// <summary>
		/// The number of update cycles to run before triggering a view update for a client (e.g., new/removed entity queries).
		/// </summary>
		public int UpdatesPerViewUpdate { get; set; } = 5;
	}
}
