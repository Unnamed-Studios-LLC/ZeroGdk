using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using ZeroGdk.Core.Network;
using ZeroGdk.Server.View;

namespace ZeroGdk.Server
{
	/// <summary>
	/// Represents a client connection to the server, encapsulating network communication and connection state.
	/// </summary>
	public sealed class Connection
	{
		private readonly INetworkClient _networkClient;

		/// <summary>
		/// Initializes a new instance of the <see cref="Connection"/> class with the specified network client and connection request.
		/// </summary>
		/// <param name="networkClient">The underlying network client used for communication.</param>
		/// <param name="request">The request containing the initial connection information.</param>
		internal Connection(INetworkClient networkClient,
			IServiceProvider services,
			OpenConnectionRequest request)
		{
			_networkClient = networkClient;
			Services = services;
			OpenRequest = request;
			TargetWorldId = request.WorldId;
			Id = request.ConnectionId;
		}

		/// <summary>
		/// Gets the unique identifier for the connection.
		/// </summary>
		public string? Id { get; }

		/// <summary>
		/// Gets or sets the name of the connection.
		/// </summary>
		public string? Name { get; set; }

		/// <summary>
		/// Gets or sets the associated entity for this connection.
		/// </summary>
		public Entity Entity { get; set; } = Entity.Null;

		/// <summary>
		/// Gets the world that this connection is currently associated with.
		/// </summary>
		public World? World { get; internal set; }

		public IServiceProvider Services { get; }

		/// <summary>
		/// Gets a value indicating whether the connection is currently active.
		/// </summary>
		public bool Connected => _networkClient.Connected;

		/// <summary>
		/// Gets the remote endpoint of the connection.
		/// </summary>
		public IPEndPoint RemoteEndPoint => _networkClient.RemoteEndPoint;

		/// <summary>
		/// Gets the original open connection request.
		/// </summary>
		internal OpenConnectionRequest OpenRequest { get; }

		internal uint TargetWorldId { get; set; } = 0;

		internal ushort BatchId { get; set; } = 0;

		internal byte ViewVersion { get; set; } = 0;

		internal EntityLists ViewEntities { get; } = new();

		internal List<ViewQuery> ViewQueries { get; } = [];

		internal List<NetworkBuffer> ReceiveList { get; } = [];

		internal string LogName => Name ?? RemoteEndPoint.ToString();

		public void AddViewQuery<T>() where T : ViewQuery
		{
			var viewQuery = Services.GetRequiredService<T>();
			AddViewQuery(viewQuery);
		}

		public void AddViewQuery<T>(T viewQuery) where T : ViewQuery
		{
			ArgumentNullException.ThrowIfNull(viewQuery);
			ViewQueries.Add(viewQuery);
		}

		/// <summary>
		/// Receives network data.
		/// </summary>
		internal void Receive()
		{
			_networkClient.Receive(ReceiveList); // TODO max receive queue size
		}

		/// <summary>
		/// Sends the specified network buffer through the connection.
		/// </summary>
		/// <param name="buffer">The <see cref="NetworkBuffer"/> to send.</param>
		internal void Send(NetworkBuffer buffer)
		{
			_networkClient.Send(buffer); // TODO max send queue size
		}

		/// <summary>
		/// Starts receiving data on the connection.
		/// </summary>
		internal void Start()
		{
			_networkClient.StartReceive();
		}

		/// <summary>
		/// Disposes the connection, closing the underlying network client and resetting its state.
		/// </summary>
		internal void Dispose()
		{
			World = null;
			Entity = Entity.Null;
			_networkClient.Close();
		}
	}
}
