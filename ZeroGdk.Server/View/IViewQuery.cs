using Arch.Core;

namespace ZeroGdk.Server
{
	public interface IViewQuery
	{
		IEnumerable<Entity> GetEntities(Connection connection);
	}
}
