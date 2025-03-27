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
	/// Processes client creation and destruction requests by handling items from the client queue.
	/// </summary>
	/// <remarks>
	/// This hosted service starts background tasks to asynchronously process client creation and destruction.
	/// </remarks>
	internal sealed class ClientFactoryProcessor(ClientQueue clientQueue,
		FactoryExecuter<Client> factoryExecuter,
		RouteResolver<Client> routeResolver,
		ClientManager clientManager,
		ILogger<ClientFactoryProcessor> logger,
		IOptions<ClientFactoryOptions> clientFactoryOptions) : IHostedService, IDisposable
	{
		private readonly ClientQueue _clientQueue = clientQueue;
		private readonly FactoryExecuter<Client> _factoryExecuter = factoryExecuter;
		private readonly RouteResolver<Client> _routeResolver = routeResolver;
		private readonly ClientManager _clientManager = clientManager;
		private readonly ILogger<ClientFactoryProcessor> _logger = logger;
		private readonly ClientFactoryOptions _clientFactoryOptions = clientFactoryOptions.Value;

		private readonly CancellationTokenSource _stopSource = new();
		private readonly SemaphoreSlim _createSemaphore = new(clientFactoryOptions.Value.CreateConcurrentProcessLimit);
		private readonly SemaphoreSlim _destroySemaphore = new(clientFactoryOptions.Value.DestroyConcurrentProcessLimit);
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
		/// Starts processing client creation and destruction requests.
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
		/// Stops processing client requests and waits for any ongoing tasks to complete.
		/// </summary>
		/// <param name="cancellationToken">A token that can signal cancellation of the stop operation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous stop operation.</returns>
		public async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogDebug("Shutting down client processor...");

			_clientQueue.Complete();

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

			// flush create, the processing can be ignored, just dispose the client
			while (_clientQueue.CreateReader.TryRead(out var pair))
			{
				var (_, client) = pair;
				client.Dispose();
			}

			// flush destroy (creates can be ignored)
			while (_clientQueue.DestroyReader.TryRead(out var client))
			{
				await _destroySemaphore.WaitAsync(cancellationToken);
				_ = DestroyAsync(client, cancellationToken);
			}

			while (RunningTasks > 0 && !cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(1_000, cancellationToken);
			}

			_logger.LogDebug("Shutdown client processor.");
		}

		/// <summary>
		/// Processes a client creation request.
		/// </summary>
		/// <param name="client">The client instance to be created.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous creation operation.</returns>
		private async Task CreateAsync(OpenClientRequest request, Client client, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _createTasks);
			bool success = false;
			try
			{
				// resolve the factory route
				if (!_routeResolver.TryResolve(request.Route, out var factoryType))
				{
					_logger.LogError("Failed to create client, no client factory at route '{route}'", request.Route);
					return;
				}

				// execute create
				try
				{
					success = await _factoryExecuter.CreateAsync(factoryType, client, request.Data, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An exception occured while creating client at route '{route}'", request.Route);
					return;
				}

				// check user set result
				if (!success)
				{
					_logger.LogError("Failed to create client at route '{route}', CreateAsync returned false", request.Route);
					return;
				}

				success = _clientManager.TryAddClient(client);
				if (!success)
				{
					_logger.LogError("Failed to add client at route '{route}'", request.Route);
					await _factoryExecuter.DestroyAsync(factoryType, client, cancellationToken).ConfigureAwait(false);
				}
			}
			finally
			{
				Interlocked.Decrement(ref _createTasks);
				_createSemaphore.Release();
				if (!success)
				{
					client.Dispose();
				}
			}
		}

		/// <summary>
		/// Processes a client destruction request.
		/// </summary>
		/// <param name="client">The client instance to be destroyed.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous destruction operation.</returns>
		private async Task DestroyAsync(Client client, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _destroyTasks);
			try
			{
				// resolve the factory route
				if (!_routeResolver.TryResolve(client.FactoryRoute, out var factoryType))
				{
					_logger.LogError("Failed to destroy client, no client factory at route '{route}'", client.FactoryRoute);
					return;
				}

				// execute create
				try
				{
					await _factoryExecuter.DestroyAsync(factoryType, client, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An exception occured while destroying client at route '{route}'", client.FactoryRoute);
				}
			}
			finally
			{
				Interlocked.Decrement(ref _destroyTasks);
				_destroySemaphore.Release();
				client.Dispose();
			}
		}

		/// <summary>
		/// Continuously processes incoming client creation requests from the queue.
		/// </summary>
		/// <param name="stoppingToken">A token that signals when to stop processing requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous processing of creation requests.</returns>
		private async Task ProcessCreateRequestsAsync(CancellationToken stoppingToken)
		{
			Client? current = null;
			try
			{
				var timedBucket = new TimedBucket(_clientFactoryOptions.CreatePerSecondLimit, TimeSpan.TicksPerSecond);
				await foreach (var (request, client) in _clientQueue.CreateReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					current = client;
					await _createSemaphore.WaitAsync(stoppingToken);
					await timedBucket.WaitAsync(stoppingToken);
					if (stoppingToken.IsCancellationRequested)
					{
						return;
					}

					_ = CreateAsync(request, client, stoppingToken);
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
		/// Continuously processes incoming client destruction requests from the queue.
		/// </summary>
		/// <param name="stoppingToken">A token that signals when to stop processing requests.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous processing of destruction requests.</returns>
		private async Task ProcessDestroyRequestsAsync(CancellationToken stoppingToken)
		{
			try
			{
				await foreach (var client in _clientQueue.DestroyReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					await _destroySemaphore.WaitAsync(stoppingToken);
					_ = DestroyAsync(client, stoppingToken);
				}
			}
			catch (OperationCanceledException)
			{
				// ignore
			}
		}
	}
}
