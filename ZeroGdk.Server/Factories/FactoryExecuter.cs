using Microsoft.Extensions.DependencyInjection;

namespace ZeroGdk.Server.Factories
{
	internal sealed class FactoryExecuter<T>(IServiceScopeFactory serviceScopeFactory)
	{
		private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

		public async Task<bool> CreateAsync(Type factoryType, T target, Google.Protobuf.WellKnownTypes.Any data, CancellationToken cancellationToken)
		{
			using var serviceScope = _serviceScopeFactory.CreateScope();
			var factory = (IFactory<T>)serviceScope.ServiceProvider.GetRequiredService(factoryType);
			return await factory.CreateAsync(target, data, cancellationToken).ConfigureAwait(false);
		}

		public async Task DestroyAsync(Type factoryType, T target, CancellationToken cancellationToken)
		{
			using var serviceScope = _serviceScopeFactory.CreateScope();
			var factory = (IFactory<T>)serviceScope.ServiceProvider.GetRequiredService(factoryType);
			await factory.DestroyAsync(target, cancellationToken).ConfigureAwait(false);
		}
	}
}
