using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZeroGdk.Server.Options;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server.HostedServices
{
	internal sealed class StartupService(WorldQueue worldQueue,
		IOptions<StartupOptions> options) : IHostedService
	{
		private readonly WorldQueue _worldQueue = worldQueue;
		private readonly StartupOptions _options = options.Value;

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			foreach (var request in _options.Worlds)
			{
				var response = await _worldQueue.CreateAsync(request);
				if (response.Result != WorldResult.Success)
				{
					throw new StartupException($"Startup world with ID '{request.WorldId}' failed to create with error: {response.Result}");
				}
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
