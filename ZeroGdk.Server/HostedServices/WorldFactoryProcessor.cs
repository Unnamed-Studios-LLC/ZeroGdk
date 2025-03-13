using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZeroGdk.Server.Factories;
using ZeroGdk.Server.Grpc;
using ZeroGdk.Server.Queues;
using ZeroGdk.Server.Routing;

namespace ZeroGdk.Server.HostedServices
{
	/// <summary>
	/// Processes the creation and destruction of worlds by handling requests from a world queue.
	/// </summary>
	/// <remarks>
	/// This hosted service starts background tasks to asynchronously process creation and destruction requests.
	/// </remarks>
	internal sealed class WorldFactoryProcessor(WorldQueue worldQueue,
		FactoryExecuter<World> factoryExecuter,
		RouteResolver<World> routeResolver,
		WorldManager worldManager,
		ILogger<WorldFactoryProcessor> logger,
		IOptions<WorldFactoryOptions> options) : IHostedService, IDisposable
	{
		private readonly WorldQueue _worldQueue = worldQueue;
		private readonly FactoryExecuter<World> _factoryExecuter = factoryExecuter;
		private readonly RouteResolver<World> _routeResolver = routeResolver;
		private readonly WorldManager _worldManager = worldManager;
		private readonly ILogger<WorldFactoryProcessor> _logger = logger;
		private readonly WorldFactoryOptions _options = options.Value;

		private readonly CancellationTokenSource _stopSource = new();
		private readonly SemaphoreSlim _createSemaphore = new(options.Value.CreateConcurrentProcessLimit);
		private readonly SemaphoreSlim _destroySemaphore = new(options.Value.DestroyConcurrentProcessLimit);
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
		/// Starts processing world creation and destruction requests in the background.
		/// </summary>
		/// <param name="cancellationToken">A token that can signal cancellation of the start operation.</param>
		/// <returns>A completed task once the background processing tasks have been initiated.</returns>
		public Task StartAsync(CancellationToken cancellationToken)
		{
			_createProcessing = ProcessCreateRequestsAsync(_stopSource.Token);
			_destroyProcessing = ProcessDestroyRequestsAsync(_stopSource.Token);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Stops processing world requests and waits for any ongoing tasks to complete.
		/// </summary>
		/// <param name="cancellationToken">A token that can signal cancellation of the stop operation.</param>
		/// <returns>A task that represents the asynchronous stop operation.</returns>
		public async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogDebug("Shutting down world processor...");

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

			// flush destroy (creates can be ignored)
			while (_worldQueue.DestroyReader.TryRead(out var pair))
			{
				_ = DestroyAsync(pair.Item1, pair.Item2, cancellationToken);
			}

			while (RunningTasks > 0 && !cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(1_000, cancellationToken);
			}

			_logger.LogDebug("Shutdown world processor.");
		}

		/// <summary>
		/// Asynchronously processes a world creation request.
		/// </summary>
		/// <param name="request">The request containing data for creating a world.</param>
		/// <param name="completion">A <see cref="TaskCompletionSource{CreateWorldResponse}"/> to signal the result of the creation operation.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>A task representing the asynchronous creation operation.</returns>
		private async Task CreateAsync(CreateWorldRequest request, TaskCompletionSource<CreateWorldResponse> completion, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _createTasks);
			try
			{
				var response = await DoCreateAsync(request, cancellationToken).ConfigureAwait(false);
				completion.SetResult(response);
			}
			catch (Exception e)
			{
				completion.SetException(e);
			}
			finally
			{
				Interlocked.Decrement(ref _createTasks);
				_createSemaphore.Release();
			}
		}

		/// <summary>
		/// Asynchronously processes a world destruction request.
		/// </summary>
		/// <param name="request">The request containing the ID of the world to destroy.</param>
		/// <param name="completion">A <see cref="TaskCompletionSource{DestroyWorldResponse}"/> to signal the result of the destruction operation.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>A task representing the asynchronous destruction operation.</returns>
		private async Task DestroyAsync(DestroyWorldRequest request, TaskCompletionSource<DestroyWorldResponse> completion, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _destroyTasks);
			try
			{
				var response = await DoDestroyAsync(request, cancellationToken).ConfigureAwait(false);
				completion.SetResult(response);
			}
			catch (Exception e)
			{
				completion.SetException(e);
			}
			finally
			{
				Interlocked.Decrement(ref _destroyTasks);
				_destroySemaphore.Release();
			}
		}

		/// <summary>
		/// Executes the creation logic for a world.
		/// </summary>
		/// <param name="request">The request containing the world ID, route, and any additional data.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>
		/// A <see cref="Task{CreateWorldResponse}"/> that represents the asynchronous creation operation.
		/// The task result contains a <see cref="CreateWorldResponse"/> indicating the outcome.
		/// </returns>
		private async Task<CreateWorldResponse> DoCreateAsync(CreateWorldRequest request, CancellationToken cancellationToken)
		{
			if (request.WorldId == 0)
			{
				_logger.LogError("Failed to add {worldId}, world ID taken", request.WorldId);
				return new CreateWorldResponse
				{
					Result = WorldResult.InternalError
				};
			}

			if (_worldManager.TryGetWorld(request.WorldId, out _))
			{
				_logger.LogError("Failed to create world {worldId}, world ID taken", request.WorldId);
				return new CreateWorldResponse
				{
					Result = WorldResult.WorldIdTaken
				};
			}

			// create and init world
			World world;
			try
			{
				world = _worldManager.CreateWorld(request.WorldId, request.Route);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Failed to create world {worldId}", request.WorldId);
				return new CreateWorldResponse
				{
					Result = WorldResult.InternalError
				};
			}

			bool added = false;
			try
			{
				// resolve the factory route
				if (!_routeResolver.TryResolve(request.Route, out var factoryType))
				{
					_logger.LogError("Failed to create world {worldId}, no world factory at route '{route}'", request.WorldId, request.Route);
					return new CreateWorldResponse
					{
						Result = WorldResult.RouteNotFound
					};
				}

				// execute create
				bool factorySuccess;
				try
				{
					factorySuccess = await _factoryExecuter.CreateAsync(factoryType, world, request.Data, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An error occured while creating world {worldId}", request.WorldId);
					return new CreateWorldResponse
					{
						Result = WorldResult.FactoryFailure
					};
				}

				// check user set result
				if (!factorySuccess)
				{
					_logger.LogError("Failed to create world {worldId} at route '{route}', CreateAsync returned false", request.WorldId, request.Route);
					return new CreateWorldResponse
					{
						Result = WorldResult.FactoryFailure
					};
				}

				world.Start();

				added = _worldManager.TryAddWorld(world);

				// should realistically never not add, but we should cover it in-case
				if (!added)
				{
					_logger.LogError("Failed to add {worldId}, world ID taken", request.WorldId);

					// stop world
					world.Stop();

					// destroy the world now
					try
					{
						await _factoryExecuter.DestroyAsync(factoryType, world, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						_logger.LogError(e, "An error occured while destroying world {worldId}", request.WorldId);
					}

					return new CreateWorldResponse
					{
						Result = WorldResult.WorldIdTaken
					};
				}

				_logger.LogInformation("Created world '{worldId}' at route '{route}'", request.WorldId, request.Route);
				return new CreateWorldResponse
				{
					Result = WorldResult.Success
				};
			}
			finally
			{
				if (!added)
				{
					world.Dispose();
				}
			}
		}

		/// <summary>
		/// Executes the destruction logic for a world.
		/// </summary>
		/// <param name="request">The request containing the world ID to be destroyed.</param>
		/// <param name="cancellationToken">A token to observe cancellation requests.</param>
		/// <returns>
		/// A <see cref="Task{DestroyWorldResponse}"/> that represents the asynchronous destruction operation.
		/// The task result contains a <see cref="DestroyWorldResponse"/> indicating the outcome.
		/// </returns>
		private async Task<DestroyWorldResponse> DoDestroyAsync(DestroyWorldRequest request, CancellationToken cancellationToken)
		{
			var world = _worldManager.RemoveWorld(request.WorldId);
			if (world == null)
			{
				_logger.LogError("Failed to destroy world {worldId}, world missing!", request.WorldId);
				return new DestroyWorldResponse
				{
					Result = WorldResult.Success
				};
			}

			// stop world
			world.Stop();

			try
			{
				// resolve the factory route
				if (world.FactoryRoute == null ||
					!_routeResolver.TryResolve(world.FactoryRoute, out var factoryType))
				{
					_logger.LogError("Failed to destroy world {worldId}, no world factory at route '{route}'", request.WorldId, world.FactoryRoute);
					return new DestroyWorldResponse
					{
						Result = WorldResult.Success
					};
				}

				// execute destroy
				try
				{
					await _factoryExecuter.DestroyAsync(factoryType, world, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An error occured while destroying world {worldId}", request.WorldId);
					return new DestroyWorldResponse
					{
						Result = WorldResult.Success
					};
				}

				_logger.LogInformation("Destroyed world '{worldId}' at route '{route}'", request.WorldId, world.FactoryRoute);
				return new DestroyWorldResponse
				{
					Result = WorldResult.Success
				};
			}
			finally
			{
				world.Dispose();
			}
		}

		/// <summary>
		/// Continuously processes incoming world creation requests from the queue.
		/// </summary>
		/// <param name="stoppingToken">A token that signals when to stop processing requests.</param>
		/// <returns>A task that represents the asynchronous processing operation.</returns>
		private async Task ProcessCreateRequestsAsync(CancellationToken stoppingToken)
		{
			try
			{
				await foreach (var pair in _worldQueue.CreateReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					await _createSemaphore.WaitAsync(stoppingToken);
					_ = CreateAsync(pair.Item1, pair.Item2, stoppingToken);
				}
			}
			catch (OperationCanceledException)
			{
				// ignore
			}
		}

		/// <summary>
		/// Continuously processes incoming world destruction requests from the queue.
		/// </summary>
		/// <param name="stoppingToken">A token that signals when to stop processing requests.</param>
		/// <returns>A task that represents the asynchronous processing operation.</returns>
		private async Task ProcessDestroyRequestsAsync(CancellationToken stoppingToken)
		{
			try
			{
				await foreach (var pair in _worldQueue.DestroyReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					await _destroySemaphore.WaitAsync(stoppingToken);
					_ = DestroyAsync(pair.Item1, pair.Item2, stoppingToken);
				}
			}
			catch (OperationCanceledException)
			{
				// ignore
			}
		}
	}
}
