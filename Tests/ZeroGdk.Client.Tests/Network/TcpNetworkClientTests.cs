using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ZeroGdk.Client.Network;

namespace ZeroGdk.Client.Tests.Network
{
	public class TcpNetworkClientTests : IDisposable
	{
		private TcpListener _listener;
		private Socket? _serverSocket;
		private Socket _clientSocket;
		private TcpNetworkClient _networkClient;
		private Thread _acceptThread;
		private int _port;

		public TcpNetworkClientTests()
		{
			// Start a listener on loopback using an ephemeral port.
			_listener = new TcpListener(IPAddress.Loopback, 0);
			_listener.Start();
			_port = ((IPEndPoint)_listener.LocalEndpoint).Port;

			// Accept the incoming connection on a separate thread.
			_acceptThread = new Thread(() =>
			{
				_serverSocket = _listener.AcceptSocket();
			});
			_acceptThread.Start();

			// Create and connect the client socket.
			_clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_clientSocket.Connect(IPAddress.Loopback, _port);

			// Ensure the server socket is accepted.
			_acceptThread.Join();

			// Create the TcpNetworkClient using the client socket.
			_networkClient = new TcpNetworkClient(_clientSocket, 100_000, 10_000);
		}

		[Fact]
		public void Send_SendsDataCorrectly()
		{
			Assert.NotNull(_serverSocket);

			// Arrange: Create a message and a network buffer to send.
			string message = "Hello, Network!";
			byte[] messageBytes = Encoding.UTF8.GetBytes(message);
			// Rent a buffer from ArrayPool (simulating real usage).
			byte[] bufferData = ArrayPool<byte>.Shared.Rent(messageBytes.Length);
			Array.Copy(messageBytes, bufferData, messageBytes.Length);
			var networkBuffer = new NetworkBuffer(bufferData, messageBytes.Length);

			// Act: Use the TcpNetworkClient to send the data.
			_networkClient.Send(networkBuffer);

			// On the server side, receive the data.
			byte[] received = new byte[messageBytes.Length];
			int totalReceived = 0;
			while (totalReceived < messageBytes.Length)
			{
				int bytesReceived = _serverSocket.Receive(received, totalReceived, messageBytes.Length - totalReceived, SocketFlags.None);
				if (bytesReceived == 0)
				{
					break;
				}
				totalReceived += bytesReceived;
			}
			string receivedMessage = Encoding.UTF8.GetString(received, 0, totalReceived);

			// Assert: Verify the sent and received messages match.
			Assert.Equal(message, receivedMessage);
		}

		[Fact]
		public void Receive_ReceivesDataCorrectly()
		{
			Assert.NotNull(_serverSocket);

			// Arrange:
			// The client’s receive logic expects an initial 4-byte length prefix.
			string message = "Hello, Client!";
			byte[] messageBytes = Encoding.UTF8.GetBytes(message);
			int messageLength = messageBytes.Length;
			byte[] lengthPrefix = BitConverter.GetBytes(messageLength);

			// Start the receive process on the network client.
			_networkClient.StartReceive();

			// Act: Send the length prefix and then the message from the server.
			_serverSocket.Send(lengthPrefix);
			_serverSocket.Send(messageBytes);

			// Allow time for the asynchronous receive to complete.
			List<NetworkBuffer> receivedBuffers = new List<NetworkBuffer>();
			bool received = false;
			for (int i = 0; i < 20; i++)
			{
				_networkClient.Receive(receivedBuffers);
				if (receivedBuffers.Count > 0)
				{
					received = true;
					break;
				}
				Thread.Sleep(100);
			}

			// Assert: Verify that data was received and the contents match.
			Assert.True(received, "Data was not received in the expected time.");
			var buffer = receivedBuffers[0];
			string receivedMessage = Encoding.UTF8.GetString(buffer.Data, 0, buffer.Count);
			Assert.Equal(message, receivedMessage);
		}

		public void Dispose()
		{
			try { _networkClient?.Close(); } catch { }
			try { _serverSocket?.Close(); } catch { }
			try { _clientSocket?.Close(); } catch { }
			_listener?.Stop();
		}
	}
}
