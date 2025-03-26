using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using ZeroGdk.Client;
using ZeroGdk.Client.Data;
using ZeroGdk.Client.Handlers;
using ZeroGdk.Client.Network;
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
			OpenConnectionRequest request,
			DataEncoding encoding,
			ConnectionOptions connectionOptions)
		{
			_networkClient = networkClient;
			Services = services;
			OpenRequest = request;
			TargetWorldId = request.WorldId;
			Id = request.ConnectionId;
			NetworkEngine = new NetworkEngine(connectionOptions.SendBufferSize, connectionOptions.ReceiveBufferSize, encoding, connectionOptions.PingIntervalMs, connectionOptions.MaxRemoteReceivedQueueSize);
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
		/// Gets a value indicating the connection state.
		/// </summary>
		public ConnectionState State => _networkClient.State;

		/// <summary>
		/// Gets the remote endpoint of the connection.
		/// </summary>
		public IPEndPoint RemoteEndPoint => _networkClient.RemoteEndPoint;

		/// <summary>
		/// Gets the original open connection request.
		/// </summary>
		internal OpenConnectionRequest OpenRequest { get; }

		internal int TargetWorldId { get; set; } = 0;

		internal List<NetworkBuffer> ReceiveList { get; } = [];

		internal NetworkEngine NetworkEngine { get; }

		internal EntityLists ViewEntities { get; } = new();

		internal List<IViewQuery> ViewQueries { get; } = [];

		public void AddReceiveHandler<T>() where T : INetworkHandler
		{
			var networkHandler = CreateService<T>();
			AddReceiveHandler(networkHandler);
		}

		public void AddReceiveHandler<T>(T networkHandler) where T : INetworkHandler
		{
			ArgumentNullException.ThrowIfNull(networkHandler);
			NetworkEngine.AddReceiveHandler(networkHandler);
		}

		public void AddRemoteReceivedHandler<T>() where T : INetworkHandler
		{
			var networkHandler = CreateService<T>();
			AddRemoteReceivedHandler(networkHandler);
		}

		public void AddRemoteReceivedHandler<T>(T networkHandler) where T : INetworkHandler
		{
			ArgumentNullException.ThrowIfNull(networkHandler);
			NetworkEngine.AddRemoteReceivedHandler(networkHandler);
		}

		public void AddViewQuery<T>() where T : IViewQuery
		{
			var viewQuery = CreateService<T>();
			AddViewQuery(viewQuery);
		}

		public void AddViewQuery<T>(T viewQuery) where T : IViewQuery
		{
			ArgumentNullException.ThrowIfNull(viewQuery);
			ViewQueries.Add(viewQuery);
		}

		public override string ToString()
		{
			return Name ?? RemoteEndPoint.ToString();
		}

		internal T CreateService<T>()
		{
			var type = typeof(T);
			var created = ActivatorUtilities.CreateInstance(Services, type, this);
			if (created is not T typed)
			{
				throw new InvalidOperationException($"Unable to create InputHandler of type '{type.FullName}'");
			}
			return typed;
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
	}
}
