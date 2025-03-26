using System;

namespace ZeroGdk.Core.Handlers
{
	public interface INetworkHandler
	{
		bool BeginBatch(int worldId, ushort batchId, long time, long remoteTime, out bool readBatch);
		bool BeginEntity(int entityId);
		bool EndBatch(ushort batchId);
		bool RemoveEntities(ReadOnlySpan<int> entities);
	}
}
