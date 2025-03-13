using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ZeroGdk.Server.Factories;
using ZeroGdk.Server.Queues;
using ZeroGdk.Server.Routing;
using ZeroGdk.Server.Utils;

namespace ZeroGdk.Server.HostedServices
{
	/// <summary>
	/// Processes connection creation and destruction requests by handling items from the connection queue.
	/// </summary>
	/// <remarks>
	/// This hosted service starts background tasks to asynchronously process connection creation and destruction.
	/// </remarks>
	internal sealed class ConnectionFactoryProcessor(ConnectionQueue connectionQueue,
		FactoryExecuter<Connection> factoryExecuter,
		RouteResolver<Connection> routeResolver,
		ConnectionManager connectionManager,
		ILogger<ConnectionFactoryProcessor> logger,
		IOptions<ConnectionFactoryOptions> connectionOptions) : IHostedService, IDisposable
	{
		private readonly ConnectionQueue _connectionQueue = connectionQueue;
		private readonly FactoryExecuter<Connection> _factoryExecuter = factoryExecuter;
		private readonly RouteResolver<Connection> _routeResolver = routeResolver;
		private readonly ConnectionManager _connectionManager = connectionManager;
		private readonly ILogger<ConnectionFactoryProcessor> _logger = logger;
		private readonly ConnectionFactoryOptions _connectionOptions = connectionOptions.Value;

		private readonly CancellationTokenSource _stopSource = new();
		private readonly SemaphoreSlim _createSemaphore = new(connectionOptions.Value.CreateConcurrentProcessLimit);
		private readonly SemaphoreSlim _destroySemaphore = new(connectionOptions.Value.DestroyConcurrentProcessLimit);
		private int _createTasks = 0;
		private int _destroyTasks = 0;
		private Task? _createProcessing;
		private Task? _destroyProcessing;

		private int RunningTasks => _createTasks + _destroyTasks;

		/// <summary>
		/// Dispose resources
		/// </summary>
		public void Dispose()
		{
			_stopSource.Dispose();
		}

		/// <summary>
		/// Starts processing connection creation and destruction requests.
		/// </summary>
		/// <param name="cancellationToken">A token that can signal cancellation of the start operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous start operation.</returns>
		public Task StartAsync(CancellationToken cancellationToken)
		{
			_createProcessing = ProcessCreateRequestsAsync(_stopSource.Token);
			_destroyProcessing = ProcessDestroyRequestsAsync(_stopSource.Token);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Stops processing connection requests and waits for any ongoing tasks to complete.
		/// </summary>
		/// <param name="cancellationToken">A token that can signal cancellation of the stop operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous stop operation.</returns>
		public async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogDebug("Shutting down connection processor...");

			_connectionQueue.Complete();

			// cancel and await processor shutdown
			_stopSource.Cancel();
			if (_createProcessing != null)
			{
				await _createProcessing.ConfigureAwait(false);
			}
			if (_destroyProcessing != null)
			{
				await _destroyProcessing.ConfigureAwait(false);
			}

			// flush create, the processing can be ignored, just dispose the connection
			while (_connectionQueue.CreateReader.TryRead(out var connection))
			{
				connection.Dispose();
			}

			// flush destroy (creates can be ignored)
			while (_connectionQueue.DestroyReader.TryRead(out var connection))
			{
				await _destroySemaphore.WaitAsync(cancellationToken);
				_ = DestroyAsync(connection, cancellationToken);
			}

			while (RunningTasks > 0 && !cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(1_000, cancellationToken);
			}

			_logger.LogDebug("Shutdown connection processor.");
		}

		/// <summary>
		/// Processes a connection creation request.
		/// </summary>
		/// <param name="connection">The connection instance to be created.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous creation operation.</returns>
		private async Task CreateAsync(Connection connection, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _createTasks);
			bool success = false;
			try
			{
				var request = connection.OpenRequest;

				// resolve the factory route
				if (!_routeResolver.TryResolve(request.Route, out var factoryType))
				{
					_logger.LogError("Failed to create connection, no connection factory at route '{route}'", request.Route);
					return;
				}

				// execute create
				try
				{
					success = await _factoryExecuter.CreateAsync(factoryType, connection, request.Data, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An exception occured while creating connection at route '{route}'", request.Route);
					return;
				}

				// check user set result
				if (!success)
				{
					_logger.LogError("Failed to create connection at route '{route}', CreateAsync returned false", request.Route);
					return;
				}

				success = _connectionManager.TryAddConnection(connection);
				if (!success)
				{
					_logger.LogError("Failed to add connection at route '{route}'", request.Route);
					await _factoryExecuter.DestroyAsync(factoryType, connection, cancellationToken).ConfigureAwait(false);
				}
			}
			finally
			{
				Interlocked.Decrement(ref _createTasks);
				_createSemaphore.Release();
				if (!success)
				{
					connection.Dispose();
				}
			}
		}

		/// <summary>
		/// Processes a connection destruction request.
		/// </summary>
		/// <param name="connection">The connection instance to be destroyed.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous destruction operation.</returns>
		private async Task DestroyAsync(Connection connection, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _destroyTasks);
			try
			{
				var request = connection.OpenRequest;

				// resolve the factory route
				if (!_routeResolver.TryResolve(request.Route, out var factoryType))
				{
					_logger.LogError("Failed to destroy connection, no connection factory at route '{route}'", request.Route);
					return;
				}

				// execute create
				try
				{
					await _factoryExecuter.DestroyAsync(factoryType, connection, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An exception occured while destroying connection at route '{route}'", request.Route);
				}
			}
			finally
			{
				Interlocked.Decrement(ref _destroyTasks);
				_destroySemaphore.Release();
				connection.Dispose();
			}
		}

		/// <summary>
		/// Continuously processes incoming connection creation requests from the queue.
		/// </summary>
		/// <param name="stoppingToken">A token that signals when to stop processing requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous processing of creation requests.</returns>
		private async Task ProcessCreateRequestsAsync(CancellationToken stoppingToken)
		{
			Connection? current = null;
			try
			{
				var timedBucket = new TimedBucket(_connectionOptions.CreatePerSecondLimit, TimeSpan.TicksPerSecond);
				await foreach (var connection in _connectionQueue.CreateReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					current = connection;
					await _createSemaphore.WaitAsync(stoppingToken);
					await timedBucket.WaitAsync(stoppingToken);
					if (stoppingToken.IsCancellationRequested)
					{
						return;
					}

					_ = CreateAsync(connection, stoppingToken);
					current = null;
				}
			}
			catch (OperationCanceledException)
			{
				// ignore
			}

			current?.Dispose();
		}

		/// <summary>
		/// Continuously processes incoming connection destruction requests from the queue.
		/// </summary>
		/// <param name="stoppingToken">A token that signals when to stop processing requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous processing of destruction requests.</returns>
		private async Task ProcessDestroyRequestsAsync(CancellationToken stoppingToken)
		{
			try
			{
				await foreach (var connection in _connectionQueue.DestroyReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					await _destroySemaphore.WaitAsync(stoppingToken);
					_ = DestroyAsync(connection, stoppingToken);
				}
			}
			catch (OperationCanceledException)
			{
				// ignore
			}
		}
	}
}
