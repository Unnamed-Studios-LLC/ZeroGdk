using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace ZeroGdk.Server.Factories
{
	public abstract class Factory<TTarget> : IFactory<TTarget>
	{
		public virtual Task<bool> CreateAsync(TTarget target, CancellationToken cancellationToken) => Task.FromResult(true);

		public virtual Task DestroyAsync(TTarget target, CancellationToken cancellationToken) => Task.CompletedTask;

		async Task<bool> IFactory<TTarget>.CreateAsync(TTarget target, Any? data, CancellationToken cancellationToken)
		{
			return await CreateAsync(target, cancellationToken);
		}

		async Task IFactory<TTarget>.DestroyAsync(TTarget target, CancellationToken cancellationToken)
		{
			await DestroyAsync(target, cancellationToken);
		}
	}

	public abstract class Factory<TTarget, TData> : IFactory<TTarget> where TData : class, IMessage, new()
	{
		public virtual Task<bool> CreateAsync(TTarget target, TData? data, CancellationToken cancellationToken) => Task.FromResult(true);

		public virtual Task DestroyAsync(TTarget target, CancellationToken cancellationToken) => Task.CompletedTask;

		async Task<bool> IFactory<TTarget>.CreateAsync(TTarget target, Any? data, CancellationToken cancellationToken)
		{
			return await CreateAsync(target, data?.Unpack<TData>() ?? default, cancellationToken);
		}

		async Task IFactory<TTarget>.DestroyAsync(TTarget target, CancellationToken cancellationToken)
		{
			await DestroyAsync(target, cancellationToken);
		}
	}
}
