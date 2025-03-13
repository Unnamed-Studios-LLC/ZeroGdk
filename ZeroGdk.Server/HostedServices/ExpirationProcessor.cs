using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroGdk.Server.Network;

namespace ZeroGdk.Server.HostedServices
{
	/// <summary>
	/// A background service that periodically processes expiration for all registered <see cref="IExpire"/> instances.
	/// </summary>
	/// <remarks>
	/// This service iterates over the provided collection of <see cref="IExpire"/> objects and calls their <see cref="IExpire.Expire"/> method every second.
	/// Any exceptions thrown during expiration are logged.
	/// </remarks>
	internal sealed class ExpirationProcessor(IEnumerable<IExpire> expires,
		ILogger<ExpirationProcessor> logger) : BackgroundService
	{
		private readonly IEnumerable<IExpire> _expires = expires;
		private readonly ILogger<ExpirationProcessor> _logger = logger;

		/// <summary>
		/// Executes the expiration processing loop.
		/// </summary>
		/// <param name="stoppingToken">
		/// A token that signals when the service should stop executing.
		/// </param>
		/// <returns>
		/// A <see cref="Task"/> that represents the long-running background operation.
		/// </returns>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				while (!stoppingToken.IsCancellationRequested)
				{
					await Task.Delay(1_000, stoppingToken);
					foreach (var expire in _expires)
					{
						try
						{
							expire.Expire();
						}
						catch (Exception e)
						{
							_logger.LogError(e, "An error occured while processing expiration for {expireType}", expire.GetType().FullName);
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				foreach (var expire in _expires)
				{
					try
					{
						expire.Expire();
					}
					catch (Exception e)
					{
						_logger.LogError(e, "An error occured while processing expiration for {expireType}", expire.GetType().FullName);
					}
				}
			}
		}
	}
}
