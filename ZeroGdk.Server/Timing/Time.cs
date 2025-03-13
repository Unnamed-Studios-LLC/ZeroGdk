namespace ZeroGdk.Server
{
	/// <summary>
	/// Provides global time-related information for the simulation or server runtime, including current tick count and elapsed time in milliseconds and seconds.
	/// </summary>
	public static class Time
	{
		/// <summary>
		/// The current simulation tick. This value increments once per update cycle.
		/// </summary>
		public static long Tick { get; internal set; }

		/// <summary>
		/// The duration of the last update in milliseconds.
		/// </summary>
		public static int Delta { get; internal set; }

		/// <summary>
		/// The duration of the last update as a float in seconds.
		/// Equivalent to <see cref="Delta"/> divided by 1000.
		/// </summary>
		public static float DeltaF => Delta / 1000f;

		/// <summary>
		/// The total elapsed time since the server started, in milliseconds.
		/// </summary>
		public static long Total { get; internal set; }

		/// <summary>
		/// The total elapsed time since the server started, as a float in seconds.
		/// Equivalent to <see cref="Total"/> divided by 1000.
		/// </summary>
		public static float TotalF => Total / 1000f;
	}
}
