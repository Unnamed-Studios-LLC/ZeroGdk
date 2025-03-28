using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ZeroGdk.Client;
using ZeroGdk.Client.Blit;
using ZeroGdk.Client.Data;
using ZeroGdk.Client.Network;

namespace ZeroGdk.Client
{
	/// <summary>
	/// Represents a client for network communication with a ZeroGdk server.
	/// This class handles sending and receiving network events, managing data serialization,
	/// and coordinating registered network handlers.
	/// </summary>
	public sealed class ZeroGdkClient : IDisposable
	{
		private readonly ClientOptions _options;
		private readonly DataEncoding _encoding;
		private readonly NetworkEngine _networkEngine;
		private readonly INetworkClient _networkClient;
		private readonly List<NetworkBuffer> _receiveList = new List<NetworkBuffer>();
		private readonly List<EntityData> _writtenData = new List<EntityData>();
		private readonly Dictionary<int, EntityData> _writtenDataMap = new Dictionary<int, EntityData>();
		private readonly Stack<EntityData> _entityDataCache = new Stack<EntityData>();
		private long _time = 0;
		private long _version = 0;

		/// <summary>
		/// Initializes a new instance of the <see cref="ZeroGdkClient"/> class with the specified connection options and data encoding.
		/// </summary>
		/// <param name="options">The client options for configuring the network client.</param>
		/// <param name="encoding">The data encoding used for serialization and deserialization.</param>
		public ZeroGdkClient(ClientOptions options, DataEncoding encoding)
		{
			_options = options;
			_encoding = encoding;
			_networkEngine = new NetworkEngine(options.SendBufferSize, options.ReceiveBufferSize, encoding, options.PingIntervalMs, options.MaxRemoteReceivedQueueSize);
			_networkClient = new TcpNetworkClient(options.MaxReceiveQueueSize, options.ReceiveBufferSize);
		}

		/// <summary>
		/// Gets the current connection state of the network client.
		/// </summary>
		public ClientState State => _networkClient.State;

		/// <summary>
		/// Gets the remote endpoint associated with the network client.
		/// </summary>
		public IPEndPoint RemoteEndPoint => _networkClient.RemoteEndPoint;

		/// <summary>
		/// Registers a handler to process incoming network events.
		/// </summary>
		/// <typeparam name="T">The type of network handler implementing <see cref="INetworkHandler"/>.</typeparam>
		/// <param name="networkHandler">The network handler instance to add.</param>
		public void AddReceiveHandler<T>(T networkHandler) where T : INetworkHandler
		{
			_networkEngine.AddReceiveHandler(networkHandler);
		}

		/// <summary>
		/// Registers a handler to process network events received from remote sources.
		/// </summary>
		/// <typeparam name="T">The type of network handler implementing <see cref="INetworkHandler"/>.</typeparam>
		/// <param name="networkHandler">The network handler instance to add.</param>
		public void AddRemoteReceivedHandler<T>(T networkHandler) where T : INetworkHandler
		{
			_networkEngine.AddRemoteReceivedHandler(networkHandler);
		}

		/// <summary>
		/// Initiates a connection to the specified remote endpoint.
		/// </summary>
		/// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
		public void Connect(IPEndPoint remoteEndPoint)
		{
			_networkClient.Connect(remoteEndPoint);
		}

		/// <summary>
		/// Closes the network client and releases all associated resources.
		/// </summary>
		public void Dispose()
		{
			_networkClient.Close();
		}

		/// <summary>
		/// Queues a one-time network event containing a single instance of data for the specified entity.
		/// The event is transmitted once and is not retained.
		/// This method should be invoked between the receive and send phases of the network loop.
		/// </summary>
		/// <typeparam name="T">The type of data to be transmitted, which must be unmanaged.</typeparam>
		/// <param name="entityId">The identifier of the entity associated with the event.</param>
		/// <param name="data">The data to be transmitted.</param>
		/// <exception cref="DataNotRegisteredException">
		/// Thrown when the specified data type is not registered with the encoding.
		/// </exception>
		public void Push<T>(int entityId, in T data) where T : unmanaged
		{
			if (!_encoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			var entityData = GetOrCreate(entityId);
			entityData.WriteEvent(_version, dataType, in data);
		}

		/// <summary>
		/// Queues a one-time network event containing a span of data for the specified entity.
		/// The event is transmitted once and is not retained.
		/// This method should be invoked between the receive and send phases of the network loop.
		/// </summary>
		/// <typeparam name="T">The type of the data elements to be transmitted, which must be unmanaged.</typeparam>
		/// <param name="entityId">The identifier of the entity associated with the event.</param>
		/// <param name="dataSpan">A read-only span containing the data elements to be transmitted.</param>
		/// <exception cref="DataNotRegisteredException">
		/// Thrown when the specified data type is not registered with the encoding.
		/// </exception>
		public void Push<T>(int entityId, in ReadOnlySpan<T> dataSpan) where T : unmanaged
		{
			if (!_encoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			var entityData = GetOrCreate(entityId);
			entityData.WriteEvent(_version, dataType, in dataSpan);
		}

		/// <summary>
		/// Receives incoming network data and processes it using the registered network handlers.
		/// This method should be called at the beginning of each network loop.
		/// </summary>
		/// <param name="time">
		/// The current time value. It must be greater than or equal to the time provided in the previous call.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// Thrown when the provided time is earlier than the last recorded time.
		/// </exception>
		/// <exception cref="Exception">
		/// Thrown when the received data buffer is malformed.
		/// </exception>
		public void Receive(long time)
		{
			if (time < _time)
			{
				throw new InvalidOperationException("Time cannot be less than the previous time.");
			}

			_time = time;
			_networkClient.Receive(_receiveList);
			var success = false;
			try
			{
				for (int i = 0; i < _receiveList.Count; i++)
				{
					var buffer = _receiveList[i];
					success = _networkEngine.TryReceive(buffer, _time);
					if (!success)
					{
						if ((_networkEngine.RemoteReceivedFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
						{
							throw new Exception("Remote received data buffer is malformed.");
						}

						if ((_networkEngine.ReceiveFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
						{
							throw new Exception("Received data buffer is malformed.");
						}
						return;
					}
				}
			}
			finally
			{
				foreach (var buffer in _receiveList)
				{
					ArrayPool<byte>.Shared.Return(buffer.Data);
				}
				_receiveList.Clear();
				if (!success)
				{
					Dispose();
				}
			}
		}

		/// <summary>
		/// Flushes all queued network data and transmits it to the remote endpoint.
		/// This method should be called at the end of each network loop.
		/// </summary>
		/// <exception cref="Exception">
		/// Thrown when the sending process fails due to a malformed data buffer.
		/// </exception>
		public void Send()
		{
			bool success = false;
			try
			{
				success = _networkEngine.TrySend(
					_networkEngine.WorldId,
					_time,
					_version,
					Span<int>.Empty,
					_writtenData.Select(x => (false, x)),
					out var networkdBuffer
				);
				if (success)
				{
					_networkClient.Send(networkdBuffer);
				}
			}
			finally
			{
				_version++;
				foreach (var data in _writtenData)
				{
					data.ClearOneOff(_version);
					_entityDataCache.Push(data);
				}
				_writtenData.Clear();
				_writtenDataMap.Clear();

				if (!success)
				{
					Dispose();
				}
			}
		}

		/// <summary>
		/// Retrieves an existing <see cref="EntityData"/> instance for the specified entity,
		/// or creates a new one if none exists.
		/// </summary>
		/// <param name="entityId">The identifier of the entity.</param>
		/// <returns>
		/// An <see cref="EntityData"/> instance associated with the specified entity.
		/// </returns>
		private EntityData GetOrCreate(int entityId)
		{
			if (!_writtenDataMap.TryGetValue(entityId, out var entityData))
			{
				entityData = _entityDataCache.Count > 0 ? _entityDataCache.Pop() : new EntityData(entityId);
				entityData.EntityId = entityId;
				_writtenData.Add(entityData);
				_writtenDataMap.Add(entityId, entityData);
			}
			return entityData;
		}
	}
}
