namespace ZeroGdk.Server
{
	public sealed class WorldFactoryOptions
	{
		/// <summary>
		/// Gets or sets the maximum number of concurrent world creation processes.
		/// </summary>
		public int CreateConcurrentProcessLimit { get; set; } = 2;
		/// <summary>
		/// Gets or sets the maximum number of concurrent world destruction processes.
		/// </summary>
		public int DestroyConcurrentProcessLimit { get; set; } = 2;
	}
}
