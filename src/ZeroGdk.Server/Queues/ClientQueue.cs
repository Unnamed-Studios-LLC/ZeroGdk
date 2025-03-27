using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace ZeroGdk.Server.Queues
{
	/// <summary>
	/// Represents a queue for handling connection creation and destruction operations using channels.
	/// </summary>
	internal sealed class ClientQueue
	{
		private readonly ClientFactoryOptions _options;
		private readonly Channel<(OpenClientRequest, Client)> _createChannel;
		private readonly Channel<Client> _destroyChannel;

		/// <summary>
		/// Initializes a new instance of the <see cref="ClientQueue"/> class with the specified options.
		/// </summary>
		/// <param name="options">
		/// The <see cref="ClientFactoryOptions"/> that defines the behavior of the queue, including its maximum length.
		/// </param>
		public ClientQueue(IOptions<ClientFactoryOptions> options)
		{
			_options = options.Value;
			_createChannel = Channel.CreateBounded<(OpenClientRequest, Client)>(new BoundedChannelOptions(_options.CreateQueueMaxLength)
			{
				FullMode = BoundedChannelFullMode.DropWrite
			}, OnDrop);
			_destroyChannel = Channel.CreateUnbounded<Client>(new UnboundedChannelOptions());
		}

		/// <summary>
		/// Gets the <see cref="ChannelReader{Client}"/> for reading client creation requests.
		/// </summary>
		public ChannelReader<(OpenClientRequest, Client)> CreateReader => _createChannel.Reader;

		/// <summary>
		/// Gets the <see cref="ChannelReader{Client}"/> for reading client destruction requests.
		/// </summary>
		public ChannelReader<Client> DestroyReader => _destroyChannel.Reader;

		/// <summary>
		/// Marks the client creation channel as complete, indicating that no further items will be written.
		/// </summary>
		public void Complete()
		{
			_createChannel.Writer.Complete();
		}

		/// <summary>
		/// Attempts to enqueue a client for creation.
		/// </summary>
		/// <param name="client">The <see cref="Client"/> instance to be enqueued.</param>
		/// <returns>
		/// <c>true</c> if the client was successfully enqueued; otherwise, <c>false</c>.
		/// </returns>
		public bool Create(OpenClientRequest request, Client client)
		{
			return _createChannel.Writer.TryWrite((request, client));
		}

		/// <summary>
		/// Attempts to enqueue a client for destruction.
		/// </summary>
		/// <param name="client">The <see cref="Client"/> instance to be enqueued for destruction.</param>
		public void Destroy(Client client)
		{
			_destroyChannel.Writer.TryWrite(client);
		}

		/// <summary>
		/// Callback invoked when a client is dropped from the bounded creation channel.
		/// This method disposes the dropped client to free up resources.
		/// </summary>
		/// <param name="pair">The dropped <see cref="OpenClientRequest"/> and <see cref="Client"/> instance.</param>
		private void OnDrop((OpenClientRequest, Client) pair)
		{
			var (_, client) = pair;
			client.Dispose();
		}
	}
}
