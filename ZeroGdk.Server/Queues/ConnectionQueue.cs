using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace ZeroGdk.Server.Queues
{
	/// <summary>
	/// Represents a queue for handling connection creation and destruction operations using channels.
	/// </summary>
	internal sealed class ConnectionQueue
	{
		private readonly ConnectionFactoryOptions _options;
		private readonly Channel<Connection> _createChannel;
		private readonly Channel<Connection> _destroyChannel;

		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionQueue"/> class with the specified options.
		/// </summary>
		/// <param name="options">
		/// The <see cref="ConnectionFactoryOptions"/> that defines the behavior of the queue, including its maximum length.
		/// </param>
		public ConnectionQueue(IOptions<ConnectionFactoryOptions> options)
		{
			_options = options.Value;
			_createChannel = Channel.CreateBounded<Connection>(new BoundedChannelOptions(_options.CreateQueueMaxLength)
			{
				FullMode = BoundedChannelFullMode.DropWrite
			}, OnDrop);
			_destroyChannel = Channel.CreateUnbounded<Connection>(new UnboundedChannelOptions());
		}

		/// <summary>
		/// Gets the <see cref="ChannelReader{Connection}"/> for reading connection creation requests.
		/// </summary>
		public ChannelReader<Connection> CreateReader => _createChannel.Reader;

		/// <summary>
		/// Gets the <see cref="ChannelReader{Connection}"/> for reading connection destruction requests.
		/// </summary>
		public ChannelReader<Connection> DestroyReader => _destroyChannel.Reader;

		/// <summary>
		/// Marks the connection creation channel as complete, indicating that no further items will be written.
		/// </summary>
		public void Complete()
		{
			_createChannel.Writer.Complete();
		}

		/// <summary>
		/// Attempts to enqueue a connection for creation.
		/// </summary>
		/// <param name="connection">The <see cref="Connection"/> instance to be enqueued.</param>
		/// <returns>
		/// <c>true</c> if the connection was successfully enqueued; otherwise, <c>false</c>.
		/// </returns>
		public bool Create(Connection connection)
		{
			return _createChannel.Writer.TryWrite(connection);
		}

		/// <summary>
		/// Attempts to enqueue a connection for destruction.
		/// </summary>
		/// <param name="connection">The <see cref="Connection"/> instance to be enqueued for destruction.</param>
		public void Destroy(Connection connection)
		{
			_destroyChannel.Writer.TryWrite(connection);
		}

		/// <summary>
		/// Callback invoked when a connection is dropped from the bounded creation channel.
		/// This method disposes the dropped connection to free up resources.
		/// </summary>
		/// <param name="connection">The dropped <see cref="Connection"/> instance.</param>
		private void OnDrop(Connection connection)
		{
			connection.Dispose();
		}
	}
}
