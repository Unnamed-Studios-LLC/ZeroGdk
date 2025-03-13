using Arch.Core.External;
using Microsoft.Extensions.Logging;
using System.Buffers;
using ZeroGdk.Core.Messages;
using ZeroGdk.Core.Network;
using ZeroGdk.Core.Blit;

namespace ZeroGdk.Server.Managers
{
	internal sealed class NetworkManager(ConnectionManager connectionManager,
		ExternalOptions externalOptions,
		ILogger<NetworkManager> logger)
	{
		private readonly ConnectionManager _connectionManager = connectionManager;
		private readonly ExternalOptions _externalOptions = externalOptions;
		private readonly ILogger<NetworkManager> _logger = logger;

		public void Receive()
		{
			try
			{
				_externalOptions.AllowChanges = false;
				Parallel.ForEach(_connectionManager.Connections, ReceiveData);
			}
			finally
			{
				_externalOptions.AllowChanges = true;
			}
		}

		public void Send()
		{
			try
			{
				_externalOptions.AllowChanges = false;
				Parallel.ForEach(_connectionManager.Connections, SendData);
			}
			finally
			{
				_externalOptions.AllowChanges = true;
			}
		}

		private void ReceiveData(Connection connection)
		{
			using var scope = GameSynchronizationContext.CreateScope();
			connection.Receive();
		}

		private unsafe void SendData(Connection connection)
		{
			var world = connection.World;
			var entities = connection.ViewEntities;
			var viewVersion = connection.ViewVersion;
			var tick = Time.Tick;
			if (world == null)
			{
				return;
			}

			// get write buffer
			const int sendBufferSize = 65536;
			var writeBuffer = ArrayPool<byte>.Shared.Rent(sendBufferSize);
			var success = false;
			try
			{
				fixed (byte* bufferPtr = writeBuffer)
				{
					var writer = new BlitWriter(bufferPtr, sendBufferSize);

					// write size, will overwrite later
					writer.Write(0u);

					// write batch header
					var batchMessage = new BatchMessage(world.WorldId, connection.BatchId++, Time.Total);
					writer.Write(in batchMessage);

					// write removed entities
					var removedBatchCount = (entities.RemovedEntities.Count + (ushort.MaxValue - 1)) / ushort.MaxValue;
					for (int i = 0; i < removedBatchCount; i++)
					{
						writer.Write((byte)MessageType.RemoveEntities);
						ushort count = Math.Min(ushort.MaxValue, (ushort)(entities.RemovedEntities.Count - i * ushort.MaxValue));
						writer.Write(count);

						for (int j = 0; j < count; j++)
						{
							var entityId = entities.RemovedEntities[i * ushort.MaxValue + j];
							if (!writer.Write(entityId))
							{
								_logger.LogError("Sent data exceeded the max send buffer");
								return;
							}
						}
					}

					// write entity updates
					writer.Write((byte)MessageType.UpdateEntities);
					foreach (var entity in entities.UniqueEntities)
					{
						var isNew = entities.NewEntities.Contains(entity.Id);
						if (!world.TryGetEntityData(entity.Id, out var data))
						{
							continue;
						}

						data.ClearOneOff(tick);

						int dataCount = (isNew ? data.PersistentWriter.DataWritten : data.PersistentChangeWriter.DataWritten) + data.EventWriter.DataWritten;
						if (dataCount > ushort.MaxValue)
						{
							_logger.LogError("Entity encountered with more than 65535 data values!");
							return;
						}

						writer.Write(entity.Id);
						writer.Write(dataCount);

						if ((isNew && !data.PersistentWriter.WriteTo(ref writer)) ||
							(!isNew && !data.PersistentChangeWriter.WriteTo(ref writer)) ||
							(!data.EventWriter.WriteTo(ref writer)))
						{
							_logger.LogError("Sent data exceeded the max send buffer");
							return;
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

					var networkBuffer = new NetworkBuffer(sendBuffer, batchLength);
					connection.Send(networkBuffer);
				}
			}
			catch (Exception e)
			{
				_logger.LogCritical(e, "An exception occurred during data write!");
			}
			finally
			{
				if (success)
				{
					connection.ViewEntities.PostSend();
				}
				else
				{
					connection.Dispose();
				}
				ArrayPool<byte>.Shared.Return(writeBuffer);
			}
		}
	}
}
