using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using ZeroGdk.Client;
using ZeroGdk.Client.Data;
using ZeroGdk.Client.Network;
using ZeroGdk.Server.View;

namespace ZeroGdk.Server
{
	/// <summary>
	/// Represents a client connection to the server, encapsulating network communication and connection state.
	/// </summary>
	public sealed class Client
	{
		private readonly INetworkClient _networkClient;

		/// <summary>
		/// Initializes a new instance of the <see cref="Client"/> class with the specified network client and connection request.
		/// </summary>
		/// <param name="networkClient">The underlying network client used for communication.</param>
		/// <param name="request">The request containing the initial connection information.</param>
		internal Client(INetworkClient networkClient,
			IServiceProvider services,
			string factoryRoute,
			string? id,
			DataEncoding encoding,
			ClientOptions clientOptions)
		{
			_networkClient = networkClient;
			Services = services;
			FactoryRoute = factoryRoute;
			Id = id;
			NetworkEngine = new NetworkEngine(clientOptions.SendBufferSize, clientOptions.ReceiveBufferSize, encoding, clientOptions.PingIntervalMs, clientOptions.MaxRemoteReceivedQueueSize);
		}

		/// <summary>
		/// Gets the unique identifier for the client.
		/// </summary>
		public string? Id { get; }

		/// <summary>
		/// Gets or sets the name of the client.
		/// </summary>
		public string? Name { get; set; }

		/// <summary>
		/// Gets or sets the associated entity for this client.
		/// </summary>
		public Entity Entity { get; set; } = Entity.Null;

		/// <summary>
		/// Gets the world that this client is currently associated with.
		/// </summary>
		public World? World { get; internal set; }

		public IServiceProvider Services { get; }

		/// <summary>
		/// Gets a value indicating the connection state.
		/// </summary>
		public ClientState State => _networkClient.State;

		/// <summary>
		/// Gets the remote endpoint of the connection.
		/// </summary>
		public IPEndPoint RemoteEndPoint => _networkClient.RemoteEndPoint;

		/// <summary>
		/// The route identifier of the factory responsible for creating this client instance.
		/// </summary>
		internal string FactoryRoute { get; }

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
		/// Disposes the client, closing the underlying network client and resetting its state.
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
		/// Sends the specified network buffer to the client.
		/// </summary>
		/// <param name="buffer">The <see cref="NetworkBuffer"/> to send.</param>
		internal void Send(NetworkBuffer buffer)
		{
			_networkClient.Send(buffer); // TODO max send queue size
		}

		/// <summary>
		/// Starts receiving data.
		/// </summary>
		internal void Start()
		{
			_networkClient.StartReceive();
		}
	}
}
