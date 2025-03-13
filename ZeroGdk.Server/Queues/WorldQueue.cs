using System.Threading.Channels;

namespace ZeroGdk.Server.Queues
{
	internal sealed class WorldQueue
	{
		private readonly Channel<(CreateWorldRequest, TaskCompletionSource<CreateWorldResponse>)> _createChannel;
		private readonly Channel<(DestroyWorldRequest, TaskCompletionSource<DestroyWorldResponse>)> _destroyChannel;

		public WorldQueue()
		{
			_createChannel = Channel.CreateUnbounded<(CreateWorldRequest, TaskCompletionSource<CreateWorldResponse>)>();
			_destroyChannel = Channel.CreateUnbounded<(DestroyWorldRequest, TaskCompletionSource<DestroyWorldResponse>)>();
		}

		public ChannelReader<(CreateWorldRequest, TaskCompletionSource<CreateWorldResponse>)> CreateReader => _createChannel.Reader;
		public ChannelReader<(DestroyWorldRequest, TaskCompletionSource<DestroyWorldResponse>)> DestroyReader => _destroyChannel.Reader;

		public async Task<CreateWorldResponse> CreateAsync(CreateWorldRequest request)
		{
			var completion = new TaskCompletionSource<CreateWorldResponse>();
			await _createChannel.Writer.WriteAsync((request, completion)).ConfigureAwait(false);
			return await completion.Task.ConfigureAwait(false);
		}

		public async Task<DestroyWorldResponse> DestroyAsync(DestroyWorldRequest request)
		{
			var completion = new TaskCompletionSource<DestroyWorldResponse>();
			await _destroyChannel.Writer.WriteAsync((request, completion)).ConfigureAwait(false);
			return await completion.Task.ConfigureAwait(false);
		}
	}
}
