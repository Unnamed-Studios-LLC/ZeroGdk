using System.Threading.Channels;
using ZeroGdk.Server.Firewall;

namespace ZeroGdk.Server.Queues
{
	internal sealed class FirewallQueue
	{
		private readonly Channel<FirewallRequest> _channel;

		public FirewallQueue()
		{
			_channel = Channel.CreateUnbounded<FirewallRequest>();
		}

		public ChannelReader<FirewallRequest> Reader => _channel.Reader;

		public ValueTask EnqueueAsync(FirewallRequest request)
		{
			return _channel.Writer.WriteAsync(request);
		}
	}
}
