using Google.Protobuf.WellKnownTypes;

namespace ZeroGdk.Server.Factories
{
	public interface IFactory<TTarget>
	{
		Task<bool> CreateAsync(TTarget target, Any? data, CancellationToken cancellationToken);

		Task DestroyAsync(TTarget target, CancellationToken cancellationToken);
	}
}
