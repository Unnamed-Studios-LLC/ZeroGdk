namespace ZeroGdk.Core
{
	public sealed class ConnectionOptions
	{
		public int MaxReceiveQueueSize { get; set; } = 50_000;
		public int MaxRemoteReceivedQueueSize { get; set; } = 500_000;
		public int PingIntervalMs { get; set; } = 5_000;
		public int ReceiveBufferSize { get; set; } = 10_000;
		public int ReceiveTimeoutMs { get; set; } = 10_000;
		public int SendBufferSize { get; set; } = 10_000;
	}
}
