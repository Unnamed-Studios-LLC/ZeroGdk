using Grpc.Net.Client;
using ZeroGdk.Server.Grpc;

namespace ZeroGdk.Server
{
	public sealed class ZeroGdkServerClient(GrpcChannel channel)
	{
		private readonly GrpcChannel _channel = channel;
		private WorkerConnection.WorkerConnectionClient? _connectionClient;
		private WorkerWorld.WorkerWorldClient? _worldClient;

		public async Task<CreateWorldResponse> CreateWorldAsync(CreateWorldRequest request, CancellationToken cancellationToken = default)
		{
			_worldClient ??= new(_channel);
			return await _worldClient.CreateAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		public async Task<DestroyWorldResponse> DestroyWorldAsync(DestroyWorldRequest request, CancellationToken cancellationToken = default)
		{
			_worldClient ??= new(_channel);
			return await _worldClient.DestroyAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}
}
