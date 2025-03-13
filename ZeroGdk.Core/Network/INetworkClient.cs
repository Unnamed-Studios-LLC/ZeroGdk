using System.Collections.Generic;
using System.Net;

namespace ZeroGdk.Core.Network
{
	internal interface INetworkClient
	{
		bool Connected { get; }
		IPEndPoint RemoteEndPoint { get; }

		void Close();
		void Receive(List<NetworkBuffer> receiveList);
		void Send(NetworkBuffer buffer);
		void StartReceive();
	}
}
