using System.Collections.Generic;
using System.Net;

namespace ZeroGdk.Client.Network
{
	internal interface INetworkClient
	{
		ClientState State { get; }
		IPEndPoint RemoteEndPoint { get; }
		NetworkErrorCodes Errors { get; }

		void Close();
		void Connect(IPEndPoint remoteEndPoint);
		void Receive(List<NetworkBuffer> receiveList);
		void Send(NetworkBuffer buffer);
		void StartReceive();
	}
}
