using Arch.Core.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZeroGdk.Server.Managers
{
	internal sealed class ViewManager(ConnectionManager connectionManager,
		IOptions<TimingOptions> timingOptions,
		ExternalOptions externalOptions,
		ILogger<ViewManager> logger)
	{
		private readonly ConnectionManager _connectionManager = connectionManager;
		private readonly ExternalOptions _externalOptions = externalOptions;
		private readonly ILogger<ViewManager> _logger = logger;
		private readonly int _connectionStepSize = timingOptions.Value.UpdatesPerViewUpdate;
		private int _connectionViewOffset;

		public void RunViewQueries()
		{
			var count = _connectionManager.Connections.Count;
			var remainder = count % _connectionStepSize;
			var connectionsToQuery = count / _connectionStepSize;
			if (_connectionViewOffset < remainder)
			{
				connectionsToQuery++;
			}

			_externalOptions.AllowChanges = false;
			try
			{
				Parallel.For(0, (int)connectionsToQuery, ViewQuery);
			}
			finally
			{
				_externalOptions.AllowChanges = true;
				_connectionViewOffset = (_connectionViewOffset + 1) % _connectionStepSize;
			}
		}

		private void ViewQuery(int parallelIndex)
		{
			using var scope = GameSynchronizationContext.CreateScope();
			var connection = _connectionManager.Connections[parallelIndex * _connectionStepSize + _connectionViewOffset];
			var entities = connection.ViewEntities;

			entities.StageEntities();
			foreach (var viewQuery in connection.ViewQueries)
			{
				try
				{
					foreach (var entity in viewQuery.GetEntities(connection))
					{
						if (entities.UniqueEntities.Add(entity) &&
							!entities.LastEntities.Contains(entity))
						{
							entities.NewEntities.Add(entity.Id);
						}
					}
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An exception occurred during '{method}' of '{type}'", nameof(viewQuery.GetEntities), viewQuery.GetType().FullName);
				}
			}

			// populate removed entities
			foreach (var entity in entities.LastEntities)
			{
				if (!entities.UniqueEntities.Contains(entity))
				{
					entities.RemovedEntities.Add(entity.Id);
				}
			}
		}
	}
}
