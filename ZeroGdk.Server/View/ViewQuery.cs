using Arch.Core;

namespace ZeroGdk.Server
{
	public abstract class ViewQuery
	{
		public abstract IEnumerable<Entity> GetEntities(Connection connection);
	}
}
