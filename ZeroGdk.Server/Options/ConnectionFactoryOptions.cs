namespace ZeroGdk.Server
{
	public sealed class ConnectionFactoryOptions
	{
		/// <summary>
		/// Gets or sets the maximum number of connection creation processes allowed per second.
		/// </summary>
		public int CreatePerSecondLimit { get; set; } = 10;
		/// <summary>
		/// Gets or sets the maximum length of the connection create queue.
		/// </summary>
		public int CreateQueueMaxLength { get; set; } = 50;
		/// <summary>
		/// Gets or sets the maximum number of concurrent connection creation processes.
		/// </summary>
		public int CreateConcurrentProcessLimit { get; set; } = 5;
		/// <summary>
		/// Gets or sets the maximum number of concurrent connection destruction processes.
		/// </summary>
		public int DestroyConcurrentProcessLimit { get; set; } = 5;
	}
}
