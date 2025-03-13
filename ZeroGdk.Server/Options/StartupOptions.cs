namespace ZeroGdk.Server.Options
{
	public sealed class StartupOptions
	{
		public List<CreateWorldRequest> Worlds { get; set; } = new();
	}
}
