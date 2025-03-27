using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ZeroGdk.Server.Managers;
using ZeroGdk.Server.Timing;

namespace ZeroGdk.Server.HostedServices
{
	/// <summary>
	/// Represents the main game server that manages game execution, update loops, and lifecycle of game managers.
	/// </summary>
	/// <remarks>
	/// The <c>GameServer</c> class implements <see cref="IHostedService"/> and starts a dedicated game thread that:
	/// <list type="bullet">
	/// <item><description>Initializes the game synchronization context.</description></item>
	/// <item><description>Starts all registered managers.</description></item>
	/// <item><description>Executes the game loop to update time variables, run scheduled tasks, and update managers.</description></item>
	/// <item><description>Stops the managers and closes the synchronization context upon shutdown.</description></item>
	/// </list>
	/// </remarks>
	internal sealed class GameServer(ITicker ticker,
		ILogger<GameServer> logger,
		IOptions<HostOptions> hostOptions,
		ClientManager clientManager,
		WorldManager worldManager,
		ViewManager viewManager,
		NetworkManager networkManager) : IHostedService
	{
		private readonly ITicker _ticker = ticker;
		private readonly ILogger<GameServer> _logger = logger;
		private readonly IOptions<HostOptions> _hostOptions = hostOptions;
		private readonly ClientManager _clientManager = clientManager;
		private readonly WorldManager _worldManager = worldManager;
		private readonly ViewManager _viewManager = viewManager;
		private readonly NetworkManager _networkManager = networkManager;
		private Thread? _executionThread;
		private bool _stopped;
		private long _stopTime;

		/// <summary>
		/// Starts the game server by launching the dedicated game execution thread.
		/// </summary>
		/// <param name="cancellationToken">A token that signals if the start operation should be canceled.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous start operation.</returns>
		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogDebug("Starting Game...");
			_executionThread = new Thread(Run)
			{
				Name = "Game Thread",
				IsBackground = false,
			};
			_executionThread.Start();
			return Task.CompletedTask;
		}

		/// <summary>
		/// Stops the game server by signaling the execution thread to terminate and waiting for it to join.
		/// </summary>
		/// <param name="cancellationToken">A token that signals if the stop operation should be canceled.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous stop operation.</returns>
		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Stopping game server...");

			_stopped = true;
			_stopTime = Stopwatch.GetTimestamp();
			_ticker.Stop();
			_executionThread?.Join();
			_logger.LogDebug("Game server stopped.");
			return Task.CompletedTask;
		}

		/// <summary>
		/// The main execution loop for the game server running on a dedicated thread.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method initializes the game synchronization context, starts all managers, and begins the ticker.
		/// It then enters a loop where it updates time variables, runs the synchronization context, and calls the
		/// update method on each manager. The loop continues until the server is signaled to stop.
		/// </para>
		/// <para>
		/// After exiting the loop, the method stops all managers and closes the synchronization context.
		/// </para>
		/// </remarks>
		private void Run()
		{
			//== Start
			GameSynchronizationContext.InitializeOnCurrentThread();
			_clientManager.Start();
			_worldManager.Start();
			_ticker.Start();

			//== Update
			_logger.LogDebug("Game is Running!");
			while (!_stopped)
			{
				// update time variables
				Time.Tick++;
				Time.Delta = _ticker.Delta;
				Time.Total = _ticker.Total;

				GameSynchronizationContext.Run();

				_clientManager.AddRemoveClients();
				_networkManager.Receive();
				_worldManager.Update();
				_viewManager.RunViewQueries();
				_networkManager.Send();

				_ticker.WaitNext();
			}

			//== Stop
			_worldManager.Stop();
			_clientManager.Stop();

			var msElapsed = (Stopwatch.GetTimestamp() - _stopTime) / TimeSpan.TicksPerMillisecond;
			var msRemaining = (int)_hostOptions.Value.ShutdownTimeout.TotalMilliseconds - msElapsed;
			if (!GameSynchronizationContext.Close((int)msRemaining))
			{
				_logger.LogError("GameSynchronizationContext failed to gracefully exit!");
			}
		}
	}
}
