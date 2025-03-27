using Arch.Core;

namespace ZeroGdk.Server
{
	public sealed class EntityNotFoundException(Entity entity) : Exception($"Entity not found: {entity}")
	{
		public Entity Entity { get; } = entity;
	}

	public sealed class WorldStartedException(World world) : Exception("This operation cannot execute after the world has started!")
	{
		public World World { get; } = world;
	}

	public sealed class StartupException(string message) : Exception(message)
	{

	}

	public sealed class DataNotFound(Type dataType) : Exception($"Data type '{dataType.FullName}' not found.")
	{
		public Type DataType { get; } = dataType;
	}
}
