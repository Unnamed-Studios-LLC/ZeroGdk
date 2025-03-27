using Arch.Core.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZeroGdk.Server.Managers
{
	internal sealed class ViewManager(ClientManager clientManager,
		IOptions<TimingOptions> timingOptions,
		ExternalOptions externalOptions,
		ILogger<ViewManager> logger)
	{
		private readonly ClientManager _clientManager = clientManager;
		private readonly ExternalOptions _externalOptions = externalOptions;
		private readonly ILogger<ViewManager> _logger = logger;
		private readonly int _clientStepSize = timingOptions.Value.UpdatesPerViewUpdate;
		private int _clientViewOffset;

		public void RunViewQueries()
		{
			var count = _clientManager.Clients.Count;
			var remainder = count % _clientStepSize;
			var clientsToQuery = count / _clientStepSize;
			if (_clientViewOffset < remainder)
			{
				clientsToQuery++;
			}

			_externalOptions.AllowChanges = false;
			try
			{
				Parallel.For(0, clientsToQuery, ViewQuery);
			}
			finally
			{
				_externalOptions.AllowChanges = true;
				_clientViewOffset = (_clientViewOffset + 1) % _clientStepSize;
			}
		}

		private void ViewQuery(int parallelIndex)
		{
			using var scope = GameSynchronizationContext.CreateScope();
			var client = _clientManager.Clients[parallelIndex * _clientStepSize + _clientViewOffset];
			var entities = client.ViewEntities;

			entities.StageEntities();
			foreach (var viewQuery in client.ViewQueries)
			{
				try
				{
					foreach (var entity in viewQuery.GetEntities(client))
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
