using Microsoft.Extensions.Hosting;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server.HostedServices
{
	internal sealed class DummyFirewallProcessor(FirewallQueue queue) : BackgroundService
	{
		private readonly FirewallQueue _queue = queue;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
			{
				// do nothing...
			}
		}

	}
}
