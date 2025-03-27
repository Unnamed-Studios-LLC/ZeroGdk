using Arch.Core.External;
using Microsoft.Extensions.Logging;
using System.Buffers;
using ZeroGdk.Client.Blit;
using ZeroGdk.Client.Data;
using ZeroGdk.Server.View;
using System.Runtime.InteropServices;

namespace ZeroGdk.Server.Managers
{
	internal sealed class NetworkManager(ClientManager clientManager,
		ExternalOptions externalOptions,
		ILogger<NetworkManager> logger)
	{
		private readonly ClientManager _clientManager = clientManager;
		private readonly ExternalOptions _externalOptions = externalOptions;
		private readonly ILogger<NetworkManager> _logger = logger;

		public void Receive()
		{
			try
			{
				_externalOptions.AllowChanges = false;
				Parallel.ForEach(_clientManager.Clients, ReceiveData);
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
				Parallel.ForEach(_clientManager.Clients, SendData);
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

		private unsafe void ReceiveData(Client client)
		{
			using var scope = GameSynchronizationContext.CreateScope();
			client.Receive();

			var networkEngine = client.NetworkEngine;
			var success = false;
			try
			{
				for (int i = 0; i < client.ReceiveList.Count; i++)
				{
					var buffer = client.ReceiveList[i];
					success = networkEngine.TryReceive(buffer, Time.Total);
					if (!success)
					{
						return;
					}
				}
			}
			catch (Exception e)
			{
				_logger.LogCritical(e, "A critical exception occurred during data read, client: {client}", client.ToString());
			}
			finally
			{
				foreach (var buffer in client.ReceiveList)
				{
					ArrayPool<byte>.Shared.Return(buffer.Data);
				}
				client.ReceiveList.Clear();
				if (!success)
				{
					client.Dispose();
				}

				if ((networkEngine.RemoteReceivedFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
				{
					_logger.LogError("Remote received data buffer is malformed, client: {client}", client.ToString());
				}
				else if ((networkEngine.ReceiveFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
				{
					_logger.LogError("Received data buffer is malformed, client: {client}", client.ToString());
				}
			}
		}

		private unsafe void SendData(Client client)
		{
			var world = client.World;
			if (world == null)
			{
				return;
			}

			var entities = client.ViewEntities;
			var networkEngine = client.NetworkEngine;
			var success = false;
			try
			{
				var removedSpan = CollectionsMarshal.AsSpan(entities.RemovedEntities);
				success = networkEngine.TrySend(world.WorldId, Time.Total, Time.Tick, removedSpan, GetUpdatedEntities(world, entities), out var sendBuffer);
				if (success)
				{
					client.Send(sendBuffer);
				}
			}
			catch (Exception e)
			{
				_logger.LogCritical(e, "A critical exception occurred during data write, client: {client}", client.ToString());
			}
			finally
			{
				if (success)
				{
					client.ViewEntities.PostSend();
				}
				else
				{
					client.Dispose();
				}

				if ((networkEngine.SendFaults & BlitFaultCodes.CapacityExceeded) == BlitFaultCodes.CapacityExceeded)
				{
					_logger.LogError("Sent data exceeded send buffer size, client: {client}", client.ToString());
				}
			}
		}
	}
}
