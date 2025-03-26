using Grpc.Net.Client;
using ZeroGdk.Server.Grpc;

namespace ZeroGdk.Server
{
	/// <summary>
	/// Provides a gRPC client wrapper for communicating with the ZeroGdk server.
	/// This client supports operations such as creating and destroying worlds and opening connections to workers.
	/// </summary>
	/// <param name="channel">The gRPC channel used for server communication.</param>
	public sealed class ZeroGdkServerClient(GrpcChannel channel)
	{
		private readonly GrpcChannel _channel = channel;
		private WorkerConnection.WorkerConnectionClient? _connectionClient;
		private WorkerWorld.WorkerWorldClient? _worldClient;

		/// <summary>
		/// Asynchronously creates a new world on the server.
		/// </summary>
		/// <param name="request">A <see cref="CreateWorldRequest"/> object containing the world creation parameters.</param>
		/// <param name="cancellationToken">An optional token to observe for cancellation.</param>
		/// <returns>
		/// A task representing the asynchronous operation, with a <see cref="CreateWorldResponse"/> containing the result.
		/// </returns>
		public async Task<CreateWorldResponse> CreateWorldAsync(CreateWorldRequest request, CancellationToken cancellationToken = default)
		{
			_worldClient ??= new(_channel);
			return await _worldClient.CreateAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Asynchronously destroys an existing world on the server.
		/// </summary>
		/// <param name="request">A <see cref="DestroyWorldRequest"/> object containing the identifier of the world to be destroyed.</param>
		/// <param name="cancellationToken">An optional token to observe for cancellation.</param>
		/// <returns>
		/// A task representing the asynchronous operation, with a <see cref="DestroyWorldResponse"/> containing the result.
		/// </returns>
		public async Task<DestroyWorldResponse> DestroyWorldAsync(DestroyWorldRequest request, CancellationToken cancellationToken = default)
		{
			_worldClient ??= new(_channel);
			return await _worldClient.DestroyAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Asynchronously opens a connection to a worker on the server.
		/// </summary>
		/// <param name="request">An <see cref="OpenConnectionRequest"/> object containing connection parameters.</param>
		/// <param name="cancellationToken">An optional token to observe for cancellation.</param>
		/// <returns>
		/// A task representing the asynchronous operation, with an <see cref="OpenConnectionResponse"/> containing the result.
		/// </returns>
		public async Task<OpenConnectionResponse> OpenConnectionAsync(OpenConnectionRequest request, CancellationToken cancellationToken = default)
		{
			_connectionClient ??= new(_channel);
			return await _connectionClient.OpenAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}
}
