namespace ZeroGdk.Server
{
	public sealed class ClientFactoryOptions
	{
		/// <summary>
		/// Gets or sets the maximum number of client creation processes allowed per second.
		/// </summary>
		public int CreatePerSecondLimit { get; set; } = 10;
		/// <summary>
		/// Gets or sets the maximum length of the client create queue.
		/// </summary>
		public int CreateQueueMaxLength { get; set; } = 50;
		/// <summary>
		/// Gets or sets the maximum number of concurrent client creation processes.
		/// </summary>
		public int CreateConcurrentProcessLimit { get; set; } = 5;
		/// <summary>
		/// Gets or sets the maximum number of concurrent client destruction processes.
		/// </summary>
		public int DestroyConcurrentProcessLimit { get; set; } = 5;
	}
}
