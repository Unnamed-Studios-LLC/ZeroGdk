using Arch.Core;

namespace ZeroGdk.Server.View
{
	internal sealed class EntityLists
	{
		public HashSet<Entity> UniqueEntities { get; private set; } = new(100);
		public HashSet<Entity> LastEntities { get; private set; } = new(100);
		public List<int> NewEntities { get; } = new(100);
		public List<int> RemovedEntities { get; } = new(100);

		public void Clear()
		{
			UniqueEntities.Clear();
			LastEntities.Clear();
			NewEntities.Clear();
			RemovedEntities.Clear();
		}

		public void StageEntities()
		{
			(UniqueEntities, LastEntities) = (LastEntities, UniqueEntities);
			UniqueEntities.Clear();
		}

		public void PostSend()
		{
			RemovedEntities.Clear();
			NewEntities.Clear();
		}
	}
}
