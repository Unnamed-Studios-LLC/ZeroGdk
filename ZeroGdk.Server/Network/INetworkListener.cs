using ZeroGdk.Core.Network;
using ZeroGdk.Server.Grpc;

namespace ZeroGdk.Server.Network
{
	internal interface INetworkListener
	{
		void ReceiveConnections(List<Connection> receiveList);

		void Start();

		void Stop();

		void Update();
	}
}
