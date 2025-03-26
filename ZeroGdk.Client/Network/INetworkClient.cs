using System.Collections.Generic;
using System.Net;

namespace ZeroGdk.Client.Network
{
	internal interface INetworkClient
	{
		ConnectionState State { get; }
		IPEndPoint RemoteEndPoint { get; }
		NetworkErrorCodes Errors { get; }

		void Close();
		void Connect(IPEndPoint remoteEndPoint);
		void Receive(List<NetworkBuffer> receiveList);
		void Send(NetworkBuffer buffer);
		void StartReceive();
	}
}
