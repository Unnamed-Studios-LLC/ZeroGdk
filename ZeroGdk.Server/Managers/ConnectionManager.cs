using Microsoft.Extensions.Logging;
using System.Net;
using ZeroGdk.Client.Network;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server
{
	internal sealed class ConnectionManager(WorldManager worldManager,
		ILogger<ConnectionManager> logger,
		ConnectionQueue connectionQueue)
	{
		private readonly WorldManager _worldManager = worldManager;
		private readonly ILogger<ConnectionManager> _logger = logger;
		private readonly ConnectionQueue _connectionQueue = connectionQueue;
		private readonly List<Connection> _connections = [];
		private readonly List<Connection> _pendingConnections = [];
		private readonly Dictionary<IPAddress, List<Connection>> _ipEndPointMap = [];
		private readonly Dictionary<string, Connection> _idMap = [];

		private bool _stopped = false;

		public IReadOnlyList<Connection> Connections => _connections;

		public void AddRemoveConnections()
		{
			// add pending connections
			lock (_pendingConnections)
			{
				foreach (var connection in _pendingConnections)
				{
					if (!_worldManager.TryGetWorld(connection.TargetWorldId, out var world))
					{
						_connectionQueue.Destroy(connection);
						continue;
					}

					if (connection.Id != null &&
						!_idMap.TryAdd(connection.Id, connection))
					{
						_connectionQueue.Destroy(connection);
						continue;
					}

					_connections.Add(connection);
				}
				_pendingConnections.Clear();
			}

			// remove connections
			for (int i = 0; i < _connections.Count; i++)
			{
				var connection = _connections[i];
				if (connection.State != ConnectionState.Connected)
				{
					_connectionQueue.Destroy(connection);
					_connections.RemoveAt(i);
					i--;
				}
			}
		}

		public void Start()
		{

		}

		public void Stop()
		{
			lock (_pendingConnections)
			{
				_stopped = true;
			}

			foreach (var connection in _pendingConnections)
			{
				_connectionQueue.Destroy(connection);
			}
			_pendingConnections.Clear();

			foreach (var connection in _connections)
			{
				_connectionQueue.Destroy(connection);
			}
			_idMap.Clear();
			_connections.Clear();
		}

		public bool TryAddConnection(Connection connection)
		{
			lock (_pendingConnections)
			{
				if (_stopped)
				{
					return false;
				}
				_pendingConnections.Add(connection);
			}
			return true;
		}
	}
}
