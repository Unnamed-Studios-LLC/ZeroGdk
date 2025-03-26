using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Sockets;
using ZeroGdk.Client;
using ZeroGdk.Client.Data;
using ZeroGdk.Client.Network;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server.Network
{
	internal sealed class TcpNetworkValidator(NetworkKeyStore networkKeyStore,
		IOptions<NetworkOptions> networkOptions,
		ConnectionQueue connectionQueue,
		IServiceProvider serviceProvider,
		DataEncoding encoding,
		IOptions<ConnectionOptions> connectionOptions) : IExpire
	{
		private class PendingSocket(Socket socket, DateTime ttlUtc, SocketAsyncEventArgs args)
		{
			public Socket Socket { get; } = socket;
			public DateTime TtlUtc { get; set; } = ttlUtc;
			public SocketAsyncEventArgs Args { get; } = args;
			public int ReceivedBytes { get; set; } = 0;
		}

		private readonly NetworkKeyStore _networkKeyStore = networkKeyStore;
		private readonly NetworkOptions _networkOptions = networkOptions.Value;
		private readonly ConnectionQueue _connectionQueue = connectionQueue;
		private readonly IServiceProvider _serviceProvider = serviceProvider;
		private readonly DataEncoding _encoding = encoding;
		private readonly ConnectionOptions _connectionOptions = connectionOptions.Value;
		private readonly HashSet<PendingSocket> _pendingSockets = [];
		private readonly ConcurrentQueue<PendingSocket> _ttlQueue = [];
		private readonly Stack<SocketAsyncEventArgs> _argsCache = [];

		public void Add(Socket socket)
		{
			PendingSocket pendingSocket;
			SocketAsyncEventArgs args;
			lock (_pendingSockets)
			{
				if (_argsCache.Count > 0)
				{
					args = _argsCache.Pop();
				}
				else
				{
					args = new SocketAsyncEventArgs();
					args.Completed += OnReceiveCompleted;
					args.SetBuffer(new byte[NetworkKey.Length]);
				}

				pendingSocket = new PendingSocket(socket, DateTime.UtcNow.Add(_networkOptions.AcceptTimeout), args);
				args.UserToken = pendingSocket;
				_pendingSockets.Add(pendingSocket);
				_ttlQueue.Enqueue(pendingSocket);
			}

			DoReceive(pendingSocket, args);
		}

		public void Expire()
		{
			var utcNow = DateTime.UtcNow;
			while (_ttlQueue.TryPeek(out var pendingSocket))
			{
				if (utcNow <= pendingSocket.TtlUtc)
				{
					return;
				}

				_ttlQueue.TryDequeue(out _);

				lock (_pendingSockets)
				{
					if (!_pendingSockets.Remove(pendingSocket))
					{
						continue;
					}
					ReturnArgs(pendingSocket.Args);
				}

				try
				{
					pendingSocket.Socket.Close();
				}
				catch (Exception)
				{
					// ignore
				}
			}
		}

		public void Stop()
		{
			var pendingSocketsToClose = new List<PendingSocket>();
			lock (_pendingSockets)
			{
				pendingSocketsToClose.AddRange(_pendingSockets);
				_pendingSockets.Clear();
			}

			foreach (var pendingSocket in pendingSocketsToClose)
			{
				try
				{
					pendingSocket.Socket.Close();
				}
				catch (Exception)
				{
					// ignore
				}
			}
		}

		private void Discard(PendingSocket pendingSocket)
		{
			lock (_pendingSockets)
			{
				if (!_pendingSockets.Remove(pendingSocket))
				{
					return;
				}
				ReturnArgs(pendingSocket.Args);
			}

			try
			{
				pendingSocket.Socket.Close();
			}
			catch (Exception)
			{
				// ignore
			}
		}

		private void DoReceive(PendingSocket pendingSocket, SocketAsyncEventArgs args)
		{
			args.SetBuffer(args.Buffer, pendingSocket.ReceivedBytes, NetworkKey.Length - pendingSocket.ReceivedBytes);

			try
			{
				// ReceiveAsync returns 'true' if the I/O is pending, 
				//   or 'false' if completed synchronously.
				var pending = pendingSocket.Socket.ReceiveAsync(args);
				if (!pending)
				{
					// If completed synchronously, handle it right now.
					OnReceiveCompleted(pendingSocket, args);
				}
			}
			catch (Exception)
			{
				Discard(pendingSocket);
			}
		}

		private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs args)
		{
			if (args.UserToken is not PendingSocket pendingSocket)
			{
				return;
			}

			if (args.SocketError != SocketError.Success ||
				args.Buffer == null)
			{
				Discard(pendingSocket);
				return;
			}

			pendingSocket.ReceivedBytes += args.BytesTransferred;
			if (pendingSocket.ReceivedBytes < NetworkKey.Length)
			{
				DoReceive(pendingSocket, args);
			}
			else
			{
				var key = Convert.ToBase64String(args.Buffer, 0, NetworkKey.Length);
				OnReceivedKey(key, pendingSocket);
			}
		}

		private void OnReceivedKey(string key, PendingSocket pendingSocket)
		{
			lock (_pendingSockets)
			{
				if (!_pendingSockets.Remove(pendingSocket))
				{
					return;
				}
				ReturnArgs(pendingSocket.Args);
			}

			if (!_networkKeyStore.TryValidate(key, out var request))
			{
				Discard(pendingSocket);
				return;
			}

			var networkClient = new TcpNetworkClient(pendingSocket.Socket, _connectionOptions.MaxReceiveQueueSize, _connectionOptions.ReceiveBufferSize);
			var connection = new Connection(networkClient, _serviceProvider, request, _encoding, _connectionOptions);
			_connectionQueue.Create(connection);
		}

		private void ReturnArgs(SocketAsyncEventArgs args)
		{
			args.UserToken = null;
			_argsCache.Push(args);
		}
	}
}
