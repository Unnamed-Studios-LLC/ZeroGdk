using Microsoft.Extensions.Logging;
using System.Net;
using ZeroGdk.Client.Network;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server
{
	internal sealed class ClientManager(WorldManager worldManager,
		ILogger<ClientManager> logger,
		ClientQueue clientQueue)
	{
		private readonly WorldManager _worldManager = worldManager;
		private readonly ILogger<ClientManager> _logger = logger;
		private readonly ClientQueue _clientQueue = clientQueue;
		private readonly List<Client> _clients = [];
		private readonly List<Client> _pendingClients = [];
		private readonly Dictionary<IPAddress, List<Client>> _ipEndPointMap = [];
		private readonly Dictionary<string, Client> _idMap = [];

		private bool _stopped = false;

		public IReadOnlyList<Client> Clients => _clients;

		public void AddRemoveClients()
		{
			// add pending clients
			lock (_pendingClients)
			{
				foreach (var client in _pendingClients)
				{
					if (!_worldManager.TryGetWorld(client.TargetWorldId, out var world))
					{
						_clientQueue.Destroy(client);
						continue;
					}

					if (client.Id != null &&
						!_idMap.TryAdd(client.Id, client))
					{
						_clientQueue.Destroy(client);
						continue;
					}

					_clients.Add(client);
				}
				_pendingClients.Clear();
			}

			// remove connections
			for (int i = 0; i < _clients.Count; i++)
			{
				var client = _clients[i];
				if (client.State != ClientState.Connected)
				{
					_clientQueue.Destroy(client);
					_clients.RemoveAt(i);
					i--;
				}
			}
		}

		public void Start()
		{

		}

		public void Stop()
		{
			lock (_pendingClients)
			{
				_stopped = true;
			}

			foreach (var client in _pendingClients)
			{
				_clientQueue.Destroy(client);
			}
			_pendingClients.Clear();

			foreach (var client in _clients)
			{
				_clientQueue.Destroy(client);
			}
			_idMap.Clear();
			_clients.Clear();
		}

		public bool TryAddClient(Client client)
		{
			lock (_pendingClients)
			{
				if (_stopped)
				{
					return false;
				}
				_pendingClients.Add(client);
			}
			return true;
		}
	}
}
