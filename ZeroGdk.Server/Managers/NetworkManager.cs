using Arch.Core.External;
using Microsoft.Extensions.Logging;
using System.Buffers;
using ZeroGdk.Client.Blit;
using ZeroGdk.Client.Data;
using ZeroGdk.Server.View;
using System.Runtime.InteropServices;

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

		private static IEnumerable<(bool, EntityData)> GetUpdatedEntities(World world, EntityLists entities)
		{
			foreach (var entity in entities.UniqueEntities)
			{
				var isNew = entities.NewEntities.Contains(entity.Id);
				if (!world.TryGetEntityData(entity.Id, out var data))
				{
					continue;
				}

				yield return (isNew, data);
			}
		}

		private unsafe void ReceiveData(Connection connection)
		{
			using var scope = GameSynchronizationContext.CreateScope();
			connection.Receive();

			var networkEngine = connection.NetworkEngine;
			var success = false;
			try
			{
				for (int i = 0; i < connection.ReceiveList.Count; i++)
				{
					var buffer = connection.ReceiveList[i];
					success = networkEngine.TryReceive(buffer, Time.Total);
					if (!success)
					{
						return;
					}
				}
			}
			catch (Exception e)
			{
				_logger.LogCritical(e, "A critical exception occurred during data read, connection: {connection}", connection.ToString());
			}
			finally
			{
				foreach (var buffer in connection.ReceiveList)
				{
					ArrayPool<byte>.Shared.Return(buffer.Data);
				}
				connection.ReceiveList.Clear();
				if (!success)
				{
					connection.Dispose();
				}

				if ((networkEngine.RemoteReceivedFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
				{
					_logger.LogError("Remote received data buffer is malformed, connection: {connection}", connection.ToString());
				}
				else if ((networkEngine.ReceiveFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
				{
					_logger.LogError("Received data buffer is malformed, connection: {connection}", connection.ToString());
				}
			}
		}

		private unsafe void SendData(Connection connection)
		{
			var world = connection.World;
			if (world == null)
			{
				return;
			}

			var entities = connection.ViewEntities;
			var networkEngine = connection.NetworkEngine;
			var success = false;
			try
			{
				var removedSpan = CollectionsMarshal.AsSpan(entities.RemovedEntities);
				success = networkEngine.TrySend(world.WorldId, Time.Total, Time.Tick, removedSpan, GetUpdatedEntities(world, entities), out var sendBuffer);
				if (success)
				{
					connection.Send(sendBuffer);
				}
			}
			catch (Exception e)
			{
				_logger.LogCritical(e, "A critical exception occurred during data write, connection: {connection}", connection.ToString());
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

				if ((networkEngine.SendFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
				{
					_logger.LogError("Sent data exceeded send buffer size, connection: {connection}", connection.ToString());
				}
			}
		}
	}
}
