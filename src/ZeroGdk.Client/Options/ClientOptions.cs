namespace ZeroGdk.Client
{
	/// <summary>
	/// Encapsulates options for a ZeroGdk client.
	/// This class defines various parameters for network buffers and timing intervals.
	/// </summary>
	public sealed class ClientOptions
	{
		/// <summary>
		/// Gets or sets the maximum number of bytes allowed in the receive queue.
		/// </summary>
		public int MaxReceiveQueueSize { get; set; } = 50_000;

		/// <summary>
		/// Gets or sets the maximum number of bytes allowed in the remote received queue.
		/// </summary>
		public int MaxRemoteReceivedQueueSize { get; set; } = 500_000;

		/// <summary>
		/// Gets or sets the interval in milliseconds between consecutive ping messages.
		/// </summary>
		public int PingIntervalMs { get; set; } = 5_000;

		/// <summary>
		/// Gets or sets the size of the buffer used for receiving network data.
		/// </summary>
		public int ReceiveBufferSize { get; set; } = 10_000;

		/// <summary>
		/// Gets or sets the timeout duration in milliseconds for receiving data.
		/// </summary>
		public int ReceiveTimeoutMs { get; set; } = 10_000;

		/// <summary>
		/// Gets or sets the size of the buffer used for sending network data.
		/// </summary>
		public int SendBufferSize { get; set; } = 10_000;
	}
}
