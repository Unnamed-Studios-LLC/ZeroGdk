using Google.Protobuf;
using ZeroGdk.Server.Factories;

namespace ZeroGdk.Server
{
	public abstract class ClientFactory : Factory<Client>
	{

	}

	public abstract class ClientFactory<T> : Factory<Client, T> where T : class, IMessage, new()
	{

	}
}
