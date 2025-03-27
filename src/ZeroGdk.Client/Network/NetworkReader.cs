using System;
using System.Collections.Generic;
using System.Text;
using ZeroGdk.Client.Blit;
using ZeroGdk.Client.Data;
using ZeroGdk.Client.Handlers;
using ZeroGdk.Client.Messages;

namespace ZeroGdk.Client.Network
{
	/// <summary>
	/// Reads and processes network data from raw buffers, decodes batch messages, and dispatches them to registered network handlers.
	/// </summary>
	internal sealed class NetworkReader
	{
		private readonly DataEncoding _encoding;
		public readonly List<INetworkHandler> _handlers = new List<INetworkHandler>();

		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkReader"/> class using the specified data encoding.
		/// </summary>
		/// <param name="encoding">The data encoding strategy used for interpreting network messages.</param>
		public NetworkReader(DataEncoding encoding)
		{
			_encoding = encoding;
		}

		/// <summary>
		/// Gets the world identifier extracted from the last processed batch message.
		/// </summary>
		public int WorldId { get; private set; }

		/// <summary>
		/// Gets the identifier of the last received remote batch.
		/// </summary>
		public ushort RemoteBatchId { get; private set; }

		/// <summary>
		/// Gets the remote time value from the last processed batch message.
		/// </summary>
		public long RemoteTime { get; private set; }

		/// <summary>
		/// Gets the accumulated fault codes encountered during the processing of network data.
		/// </summary>
		public BlitFaultCodes Faults { get; private set; }


		/// <summary>
		/// Registers a network handler to process incoming network messages.
		/// </summary>
		/// <param name="networkHandler">The network handler to register.</param>
		public void AddHandler(INetworkHandler networkHandler)
		{
			_handlers.Add(networkHandler);
		}


		/// <summary>
		/// Attempts to receive and process the specified network buffer.
		/// This method decodes batch messages, validates message order and time, skips ping/pong messages,
		/// and dispatches the remaining data to the registered network handlers.
		/// </summary>
		/// <param name="buffer">The network buffer containing the raw data to be processed.</param>
		/// <param name="time">
		/// The current time value used for validating the sequence of batches. 
		/// It must be greater than or equal to the previously recorded remote time.
		/// </param>
		/// <returns>
		/// <c>true</c> if the buffer was successfully processed; otherwise, <c>false</c>.
		/// </returns>
		/// <remarks>
		/// This method updates the <see cref="WorldId"/>, <see cref="RemoteBatchId"/>, and <see cref="RemoteTime"/> properties 
		/// upon successful processing, and any encountered fault codes are accumulated in the <see cref="Faults"/> property.
		/// </remarks>
		public unsafe bool TryReceive(NetworkBuffer buffer, long time)
		{
			RawBlitReader reader = default;
			BatchMessage* batchMessage = default;
			byte type = default;
			int entityId = default;
			int* entityIds = default;
			ushort count = default;
			ushort spanCount = default;
			try
			{
				fixed (byte* bufferPtr = buffer.Data)
				{
					// validate batch info
					reader = new RawBlitReader(bufferPtr, buffer.Count);

					// read batch header
					// validate time
					// validate batchId
					if (!reader.Read(&batchMessage) ||
						batchMessage->Time < RemoteTime ||
						batchMessage->BatchId != RemoteBatchId + 1)
					{
						return false;
					}

					RemoteTime = batchMessage->Time;
					RemoteBatchId = batchMessage->BatchId;
					WorldId = batchMessage->WorldId;

					// skip ping/pong
					while (reader.BytesRead < reader.Capacity)
					{
						if (!reader.Read(out type))
						{
							return false;
						}

						var done = false;
						switch ((MessageType)type)
						{
							case MessageType.Ping:
							case MessageType.Pong:
								done = false;
								break;
							default:
								done = true;
								break;
						}

						if (done)
						{
							reader.Seek(reader.BytesRead - 1); // go back one
							break;
						}
					}

					// handle data
					foreach (var handler in _handlers)
					{
						if (!handler.BeginBatch(batchMessage->WorldId, batchMessage->BatchId, time, batchMessage->Time, out var readBatch))
						{
							return false;
						}

						if (readBatch)
						{
							// read message type
							if (!reader.Read(out type))
							{
								return false;
							}

							switch ((MessageType)type)
							{
								case MessageType.RemoveEntities:
									// read removed entities
									// count -> id span
									if (!reader.Read(out count) ||
										!reader.Read(&entityIds, count))
									{
										return false;
									}

									var span = new ReadOnlySpan<int>(entityIds, count);
									if (!handler.RemoveEntities(span))
									{
										return false;
									}
									break;
								case MessageType.UpdateEntities:
									// read updated entities
									// no entity count needed, we just read to the end
									while (reader.BytesRead < reader.Capacity)
									{
										if (!reader.Read(out entityId) ||
											!reader.Read(out count) ||
											!handler.BeginEntity(entityId))
										{
											return false;
										}

										for (int j = 0; j < count; j++)
										{
											if (!reader.Read(out type))
											{
												return false;
											}

											// try read span
											var isSpan = type == DataWriter.SpanFlag;
											if (isSpan &&
												(!reader.Read(out type) || !reader.Read(out spanCount)))
											{
												return false;
											}

											if (!_encoding.TryGetHandler(type, out var dataHandler))
											{
												return false;
											}

											var handleResult = isSpan ?
												dataHandler.HandleRawDataSpan(ref reader, handler, spanCount) :
												dataHandler.HandleRawData(ref reader, handler);
											if (!handleResult)
											{
												return false;
											}
										}
									}
									break;
							}

							if (!handler.EndBatch(batchMessage->BatchId))
							{
								return false;
							}
						}
					}
				}
				return true;
			}
			finally
			{
				Faults |= reader.Faults;
			}
		}
	}
}
