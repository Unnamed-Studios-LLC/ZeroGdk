using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ZeroGdk.Client.Network
{
	internal class TcpNetworkClient : INetworkClient
	{
		private Socket _socket;
		private readonly int _maxReceiveQueueSize;
		private readonly int _maxReceiveSize;
		private readonly SocketAsyncEventArgs _readArgs;
		private readonly SocketAsyncEventArgs _writeArgs;

		private readonly Queue<NetworkBuffer> _sendQueue = new Queue<NetworkBuffer>();
		private readonly List<NetworkBuffer> _receiveQueue = new List<NetworkBuffer>();
		private int _receiveQueueSize = 0;

		private bool _receivingData = false;
		private int _receivedBytes = 0;
		private NetworkBuffer _receiveBuffer;

		private bool _sending = false;
		private int _sentBytes = 0;
		private NetworkBuffer _sendingBuffer;
		private readonly object _sendLock = new object();

		private int _closed = 0;
		private bool _connecting = false;

		public TcpNetworkClient(Socket socket, int maxReceiveQueueSize, int maxReceiveSize)
		{
			_socket = socket;
			_maxReceiveQueueSize = maxReceiveQueueSize;
			_maxReceiveSize = maxReceiveSize;

			_socket.NoDelay = true;

			_readArgs = new SocketAsyncEventArgs();
			_readArgs.Completed += OnReceiveCompleted;

			_writeArgs = new SocketAsyncEventArgs();
			_writeArgs.Completed += OnSendCompleted;
		}

		public TcpNetworkClient(int maxReceiveQueueSize, int maxReceiveSize)
		{
			_maxReceiveQueueSize = maxReceiveQueueSize;
			_maxReceiveSize = maxReceiveSize;

			_readArgs = new SocketAsyncEventArgs();
			_readArgs.Completed += OnReceiveCompleted;

			_writeArgs = new SocketAsyncEventArgs();
			_writeArgs.Completed += OnSendCompleted;
		}

		public ConnectionState State => _connecting ? ConnectionState.Connecting :
			_closed == 0 && _socket.Connected ? ConnectionState.Connected :
			ConnectionState.Disconnected;
		public NetworkErrorCodes Errors { get; private set; } = NetworkErrorCodes.None;
		public IPEndPoint RemoteEndPoint => (IPEndPoint)_socket.RemoteEndPoint;

		public void Close()
		{
			if (Interlocked.CompareExchange(ref _closed, 1, 0) == 1)
			{
				return;
			}

			try
			{
				_socket.Shutdown(SocketShutdown.Both);
			}
			catch { /* ignore */ }

			try
			{
				_socket.Close();
			}
			catch { /* ignore */ }

			lock (_receiveQueue)
			{
				foreach (var buffer in _receiveQueue)
				{
					ArrayPool<byte>.Shared.Return(buffer.Data);
				}
				_receiveQueue.Clear();
			}

			lock (_sendLock)
			{
				while (_sendQueue.Count > 0)
				{
					var buffer = _sendQueue.Dequeue();
					ArrayPool<byte>.Shared.Return(buffer.Data);
				}
				_receiveQueue.Clear();
			}
		}

		public void Connect(IPEndPoint remoteEndPoint)
		{
			if (_connecting ||
				_socket != null)
			{
				throw new InvalidOperationException("Connect may only be called once.");
			}

			// Create a socket using the remote endpoint's address family.
			// Also, set NoDelay to true for immediate sending.
			_connecting = true;
			_socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true
			};

			var connectArgs = new SocketAsyncEventArgs
			{
				RemoteEndPoint = remoteEndPoint
			};
			connectArgs.Completed += OnConnectCompleted;

			try
			{
				var pending = _socket.ConnectAsync(connectArgs);
				if (!pending)
				{
					OnConnectCompleted(_socket, connectArgs);
				}
			}
			catch
			{
				Errors |= NetworkErrorCodes.ConnectionFailed;
				Close();
				_connecting = false;
				throw;
			}
		}

		public void Receive(List<NetworkBuffer> receiveList)
		{
			lock (_receiveQueue)
			{
				if (_receiveQueue.Count == 0)
				{
					return;
				}

				receiveList.AddRange(_receiveQueue);
				_receiveQueue.Clear();
				_receiveQueueSize = 0;
			}
		}

		public void Send(NetworkBuffer buffer)
		{
			if (_connecting ||
				_socket == null)
			{
				throw new InvalidOperationException("Cannot send, socket is not connected!");
			}

			lock (_sendLock)
			{
				if (_sending)
				{
					_sendQueue.Enqueue(buffer);
					return;
				}

				_sending = true;
			}

			_sendingBuffer = buffer;
			_sentBytes = 0;
			DoSend();
		}

		public void StartReceive()
		{
			if (_socket == null)
			{
				throw new InvalidOperationException("Client is not connected.");
			}

			_receiveBuffer = new NetworkBuffer(ArrayPool<byte>.Shared.Rent(4), 4);
			DoReceive();
		}

		private void DoReceive()
		{
			_readArgs.SetBuffer(_receiveBuffer.Data, _receivedBytes, _receiveBuffer.Count - _receivedBytes);

			try
			{
				// ReceiveAsync returns 'true' if the I/O is pending, 
				//   or 'false' if completed synchronously.
				var pending = _socket.ReceiveAsync(_readArgs);
				if (!pending)
				{
					// If completed synchronously, handle it right now.
					OnReceiveCompleted(this, _readArgs);
				}
			}
			catch (Exception)
			{
				ArrayPool<byte>.Shared.Return(_readArgs.Buffer);
				Close();
			}
		}

		private void DoSend()
		{
			// Set the buffer to the portion of data that is still left to send.
			_writeArgs.SetBuffer(_sendingBuffer.Data, _sentBytes, _sendingBuffer.Count - _sentBytes);
			
			try
			{
				// SendAsync returns 'true' if the I/O is pending, 
				//   or 'false' if completed synchronously.
				var pending = _socket.SendAsync(_writeArgs);
				if (!pending)
				{
					// If completed synchronously, handle it right now.
					OnSendCompleted(this, _writeArgs);
				}
			}
			catch (Exception)
			{
				ArrayPool<byte>.Shared.Return(_writeArgs.Buffer);
				Close();
			}
		}

		private void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
		{
			if (args.SocketError != SocketError.Success)
			{
				Errors |= NetworkErrorCodes.ConnectionFailed;
				Close();
				_connecting = false;
				return;
			}

			StartReceive();
			_connecting = false;
		}

		private void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
		{
			if (args.SocketError != SocketError.Success)
			{
				ArrayPool<byte>.Shared.Return(args.Buffer);
				Close();
				return;
			}

			_receivedBytes += args.BytesTransferred;
			if (_receivedBytes < _receiveBuffer.Count)
			{
				DoReceive();
			}
			else
			{
				OnReceiveCompleteSuccess();
			}
		}

		private void OnReceiveCompleteSuccess()
		{
			// if
			//	we were receiving data, queue the received buffer
			// else
			//	read the received size and allocate a buffer
			int nextSize;
			if (_receivingData)
			{
				lock (_receiveQueue)
				{
					if (_closed != 0)
					{
						return;
					}

					_receiveQueueSize += _receiveBuffer.Count;
					if (_receiveQueueSize > _maxReceiveQueueSize)
					{
						Errors |= NetworkErrorCodes.ReceiveQueueExceeded;
						ArrayPool<byte>.Shared.Return(_receiveBuffer.Data);
						_receiveBuffer = default;
						Close();
						return;
					}

					_receiveQueue.Add(_receiveBuffer);
				}
				nextSize = 4;
			}
			else
			{
				nextSize = BitConverter.ToInt32(_receiveBuffer.Data, 0);
				ArrayPool<byte>.Shared.Return(_receiveBuffer.Data);
			}

			if (nextSize > _maxReceiveSize)
			{
				Errors |= NetworkErrorCodes.ReceiveBufferExceeded;
				_receiveBuffer = default;
				Close();
				return;
			}

			_receivingData = !_receivingData;
			_receiveBuffer = new NetworkBuffer(ArrayPool<byte>.Shared.Rent(nextSize), nextSize);
			_receivedBytes = 0;

			DoReceive();
		}

		private void OnSendCompleted(object sender, SocketAsyncEventArgs args)
		{
			if (args.SocketError != SocketError.Success)
			{
				ArrayPool<byte>.Shared.Return(args.Buffer);
				Close();
				return;
			}

			_sentBytes += args.BytesTransferred;
			if (_sentBytes < _sendingBuffer.Count)
			{
				DoSend();
			}
			else
			{
				OnSendCompleteSuccess();
			}
		}

		private void OnSendCompleteSuccess()
		{
			_sentBytes = 0;
			ArrayPool<byte>.Shared.Return(_sendingBuffer.Data);
			_sendingBuffer = default;

			lock (_sendQueue)
			{
				if (_sendQueue.Count == 0)
				{
					_sending = false;
					return;
				}

				_sendingBuffer = _sendQueue.Dequeue();
			}

			DoSend();
		}
	}
}
