using Google.Protobuf;
using ZeroGdk.Server.Factories;

namespace ZeroGdk.Server
{
	public abstract class ClientFactory : Factory<Connection>
	{

	}

	public abstract class ClientFactory<T> : Factory<Connection, T> where T : class, IMessage, new()
	{

	}
}
