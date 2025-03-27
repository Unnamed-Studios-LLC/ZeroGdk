namespace ZeroGdk.Server
{
	public sealed class NetworkOptions
	{
		/// <summary>
		/// The maximum time allowed for a connecting client to send its authentication key for validation.
		/// </summary>
		public TimeSpan AcceptTimeout { get; set; } = TimeSpan.FromSeconds(5);
		/// <summary>
		/// The duration a network authentication key remains valid before expiring.
		/// </summary>
		public TimeSpan NetworkKeyTimeout { get; set; } = TimeSpan.FromSeconds(8);
		/// <summary>
		/// The IP mode the server listens on. Use <see cref="IpModes.Ipv4"/>, <see cref="IpModes.Ipv6"/>, or <see cref="IpModes.Both"/> for dual-stack mode.
		/// </summary>
		public IpModes IpModes { get; set; } = IpModes.Both;
		/// <summary>
		/// The port number the game server listens on for incoming clients.
		/// </summary>
		public int GamePort { get; set; } = 12000;
		/// <summary>
		/// The maximum number of pending clients the server will allow in the listener backlog queue.
		/// </summary>
		public int ListenBacklog { get; set; } = 50;
		/// <summary>
		/// The amount of millisends between sending pings to clients
		/// </summary>
		public int PingIntervalMs { get; set; } = 5_000;
	}
}
