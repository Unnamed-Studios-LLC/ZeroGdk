using System;
using System.Buffers;
using System.Collections.Generic;
using ZeroGdk.Core.Blit;
using ZeroGdk.Core.Data;
using ZeroGdk.Core.Handlers;
using ZeroGdk.Core.Messages;

namespace ZeroGdk.Core.Network
{
	/// <summary>
	/// Manages network operations including sending and receiving messages, processing ping/pong signals,
	/// handling entity updates and removals, and coordinating remote acknowledgment processing.
	/// </summary>
	internal sealed class NetworkEngine : IDisposable
	{
		private readonly int _sendBufferSize;
		private readonly int _receiveBufferSize;
		private readonly DataEncoding _encoding;
		private readonly int _pingIntervalMs;
		private readonly int _maxRemoteReceivedQueueSize;

		private readonly NetworkReader _reader;
		private NetworkReader _remoteReceivedReader;
		private long _lastSentPing = 0;
		private bool _pendingPing = false;
		private bool _sendPong = false;
		private int _remoteReceivedQueueSize = 0;
		private ushort _lastSentBatchId = 0;
		private HashSet<ushort> _sentKeys = null;
		private Queue<NetworkBuffer> _sentBuffers = null;

		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkEngine"/> class with the specified configuration parameters.
		/// </summary>
		/// <param name="sendBufferSize">The size of the buffer used for sending network messages.</param>
		/// <param name="receiveBufferSize">The size of the buffer used for receiving network messages.</param>
		/// <param name="encoding">The data encoding used for serialization and deserialization.</param>
		/// <param name="pingIntervalMs">The interval, in milliseconds, between consecutive pings.</param>
		/// <param name="maxRemoteReceivedQueueSize">The maximum allowed size of the remote received queue.</param>
		public NetworkEngine(int sendBufferSize,
			int receiveBufferSize,
			DataEncoding encoding,
			int pingIntervalMs,
			int maxRemoteReceivedQueueSize)
		{
			_sendBufferSize = sendBufferSize;
			_receiveBufferSize = receiveBufferSize;
			_encoding = encoding;
			_pingIntervalMs = pingIntervalMs;
			_maxRemoteReceivedQueueSize = maxRemoteReceivedQueueSize;

			_reader = new NetworkReader(encoding);
		}

		/// <summary>
		/// Gets the current batch identifier for outgoing network messages.
		/// </summary>
		public ushort BatchId { get; private set; }

		/// <summary>
		/// Gets the fault codes accumulated during the sending process.
		/// </summary>
		public BlitFaultCodes SendFaults { get; private set; }

		/// <summary>
		/// Gets the fault codes encountered during the receiving process.
		/// </summary>
		public BlitFaultCodes ReceiveFaults => _reader.Faults;

		/// <summary>
		/// Gets the fault codes related to processing remote received messages.
		/// </summary>
		public BlitFaultCodes RemoteReceivedFaults => _remoteReceivedReader?.Faults ?? BlitFaultCodes.None;

		/// <summary>
		/// Gets the measured network latency in milliseconds. Returns -1 if latency has not been determined.
		/// </summary>
		public int Latency { get; private set; } = -1;

		/// <summary>
		/// Gets a value indicating whether remote received processing is enabled.
		/// </summary>
		public bool RemoteReceivedEnabled => _remoteReceivedReader != null;

		/// <summary>
		/// Gets the world identifier from the last processed batch.
		/// </summary>
		public int WorldId => _reader.WorldId;

		/// <summary>
		/// Registers a network handler to process incoming network messages.
		/// </summary>
		/// <param name="networkHandler">The network handler to be added.</param>
		public void AddReceiveHandler(INetworkHandler networkHandler)
		{
			_reader.AddHandler(networkHandler);
		}

		/// <summary>
		/// Registers a network handler to process remote received network messages.
		/// If remote received processing is not enabled, it initializes the required components.
		/// </summary>
		/// <param name="networkHandler">The network handler to be added.</param>
		public void AddRemoteReceivedHandler(INetworkHandler networkHandler)
		{
			if (_remoteReceivedReader == null)
			{
				_remoteReceivedReader = new NetworkReader(_encoding);
				_sentKeys = new HashSet<ushort>();
				_sentBuffers = new Queue<NetworkBuffer>();
			}
			_remoteReceivedReader.AddHandler(networkHandler);
		}

		/// <summary>
		/// Releases all resources used by the <see cref="NetworkEngine"/> and returns rented buffers to the pool.
		/// </summary>
		public void Dispose()
		{
			foreach (var sentBuffer in _sentBuffers)
			{
				ArrayPool<byte>.Shared.Return(sentBuffer.Data);
			}
			_sentBuffers.Clear();
			_sentKeys.Clear();
		}

		/// <summary>
		/// Attempts to process an incoming network buffer. This method handles the ping/pong protocol,
		/// validates batch information, and delegates the remaining data processing to the underlying reader.
		/// </summary>
		/// <param name="buffer">The network buffer containing the raw data to be processed.</param>
		/// <param name="time">The current timestamp used for processing and latency measurement.</param>
		/// <returns>
		/// <c>true</c> if the buffer was successfully processed; otherwise, <c>false</c>.
		/// </returns>
		public unsafe bool TryReceive(NetworkBuffer buffer, long time)
		{
			RawBlitReader reader = default;
			BatchMessage* batchMessage = default;
			byte type = default;

			fixed (byte* bufferPtr = buffer.Data)
			{
				// validate batch info
				reader = new RawBlitReader(bufferPtr, buffer.Count);

				// read batch header
				if (!reader.Read(&batchMessage))
				{
					return false;
				}

				if (RemoteReceivedEnabled &&
					!HandleRemoteReceived(*batchMessage))
				{
					return false;
				}

				// handle ping/pong
				while (reader.BytesRead < reader.Capacity)
				{
					if (!reader.Read(out type))
					{
						return false;
					}

					switch ((MessageType)type)
					{
						case MessageType.Ping:
							_sendPong = true;
							break;
						case MessageType.Pong:
							if (_pendingPing)
							{
								_pendingPing = false;
								Latency = (int)(time - _lastSentPing);
							}
							break;
						default:
							reader.Seek(reader.BytesRead - 1); // go back one
							break;
					}
				}
			}

			if (!_reader.TryReceive(buffer, time))
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Attempts to send a network message containing batch information, ping/pong signals, removed entities,
		/// and updated entity data.
		/// </summary>
		/// <param name="worldId">The identifier of the world associated with the message.</param>
		/// <param name="time">The current timestamp used in the batch message.</param>
		/// <param name="version">The version number used for entity data validation.</param>
		/// <param name="removedEntities">A span of entity identifiers representing entities to be removed.</param>
		/// <param name="updatedEntities">
		/// A collection of tuples indicating whether an entity is new and its corresponding update data.
		/// </param>
		/// <param name="buffer">
		/// When this method returns, contains the network buffer prepared for sending if the operation is successful.
		/// </param>
		/// <returns>
		/// <c>true</c> if the network message was successfully prepared; otherwise, <c>false</c>.
		/// </returns>
		public unsafe bool TrySend(int worldId, long time, long version, Span<int> removedEntities, IEnumerable<(bool New, EntityData EntityData)> updatedEntities, out NetworkBuffer buffer)
		{
			// get write buffer
			buffer = default;
			var writeBuffer = ArrayPool<byte>.Shared.Rent(_sendBufferSize);
			BlitWriter writer = default;
			try
			{
				fixed (byte* bufferPtr = writeBuffer)
				{
					writer = new BlitWriter(bufferPtr, _sendBufferSize);

					// write size, will overwrite later
					writer.Write(0u);

					// write batch header
					var batchMessage = new BatchMessage(worldId, ++BatchId, _reader.RemoteBatchId, time);
					writer.Write(in batchMessage);

					// write ping
					if (!_pendingPing &&
						time - _lastSentPing > _pingIntervalMs)
					{
						_lastSentPing = time;
						_pendingPing = true;
						writer.Write((byte)MessageType.Ping);
					}

					// write pong
					if (_sendPong)
					{
						_sendPong = false;
						writer.Write((byte)MessageType.Pong);
					}

					// write removed entities
					var removedBatchCount = (removedEntities.Length + (ushort.MaxValue - 1)) / ushort.MaxValue;
					for (int i = 0; i < removedBatchCount; i++)
					{
						writer.Write((byte)MessageType.RemoveEntities);
						ushort count = Math.Min(ushort.MaxValue, (ushort)(removedEntities.Length - i * ushort.MaxValue));
						writer.Write(count);

						fixed (int* entityIds = &removedEntities[i * ushort.MaxValue])
						{
							if (!writer.Write(entityIds, count))
							{
								return false;
							}
						}
					}

					// write entity updates
					writer.Write((byte)MessageType.UpdateEntities);
					foreach (var (@new, data) in updatedEntities)
					{
						data.ClearOneOff(version);

						int dataCount = (@new ? data.PersistentWriter.DataWritten : data.PersistentChangeWriter.DataWritten) + data.EventWriter.DataWritten;
						if (dataCount > ushort.MaxValue)
						{
							throw new InvalidOperationException("Entity encountered with more than 65535 data values.");
						}

						writer.Write(data.EntityId);
						writer.Write(dataCount);

						if (@new && !data.PersistentWriter.WriteTo(ref writer) ||
							!@new && !data.PersistentChangeWriter.WriteTo(ref writer) ||
							!data.EventWriter.WriteTo(ref writer))
						{
							return false;
						}
					}

					// overwrite size
					var batchLength = writer.BytesWritten;
					*(int*)bufferPtr = batchLength - 4;
					var sendBuffer = ArrayPool<byte>.Shared.Rent(batchLength);
					fixed (byte* dst = sendBuffer)
					{
						Buffer.MemoryCopy(bufferPtr, dst, batchLength, batchLength);
					}

					buffer = new NetworkBuffer(sendBuffer, batchLength);
					if (RemoteReceivedEnabled)
					{
						var newRemoteReceivedQueueSize = _remoteReceivedQueueSize + buffer.Data.Length;
						if (newRemoteReceivedQueueSize > _maxRemoteReceivedQueueSize)
						{
							ArrayPool<byte>.Shared.Return(buffer.Data);
							return false;
						}

						if (!_sentKeys.Add(batchMessage.BatchId))
						{
							ArrayPool<byte>.Shared.Return(buffer.Data);
							return false;
						}
						_lastSentBatchId = batchMessage.BatchId;
						_sentBuffers.Enqueue(buffer);
					}
				}
				return true;
			}
			finally
			{
				SendFaults |= writer.Faults;
				ArrayPool<byte>.Shared.Return(writeBuffer);
			}
		}

		/// <summary>
		/// Processes a batch message for remote acknowledgment, ensuring that the remote received reader's state 
		/// is updated based on the provided batch message.
		/// </summary>
		/// <param name="batchMessage">The batch message to be handled for remote receipt acknowledgment.</param>
		/// <returns>
		/// <c>true</c> if the batch message was successfully processed; otherwise, <c>false</c>.
		/// </returns>
		private bool HandleRemoteReceived(BatchMessage batchMessage)
		{
			if (batchMessage.RemoteBatchId == _remoteReceivedReader.RemoteBatchId)
			{
				return true;
			}

			if (_sentKeys == null ||
				!_sentKeys.Contains(batchMessage.RemoteBatchId))
			{
				return false;
			}

			while (_remoteReceivedReader.RemoteBatchId != batchMessage.RemoteBatchId)
			{
				if (_sentBuffers.Count == 0)
				{
					return false;
				}

				var buffer = _sentBuffers.Dequeue();
				_remoteReceivedQueueSize -= buffer.Data.Length;
				if (!_remoteReceivedReader.TryReceive(buffer, batchMessage.Time))
				{
					return false;
				}
				_sentKeys.Remove(_remoteReceivedReader.RemoteBatchId);
			}

			return true;
		}
	}
}
