using Google.Protobuf;
using ZeroGdk.Server.Factories;

namespace ZeroGdk.Server
{
	public abstract class WorldFactory : Factory<World>
	{

	}

	public abstract class WorldFactory<T> : Factory<World, T> where T : class, IMessage, new()
	{

	}
}
