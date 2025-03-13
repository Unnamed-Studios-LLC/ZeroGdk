namespace ZeroGdk.Server
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
	public class RouteAttribute(string path) : Attribute
	{
		public string Path { get; } = path;
	}
}
