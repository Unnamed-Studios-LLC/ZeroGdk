using System.Runtime.InteropServices;

namespace ZeroGdk.Core.Messages
{
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	internal readonly struct BatchMessage
	{
		[FieldOffset(0)]
		public readonly int WorldId;
		[FieldOffset(4)]
		public readonly ushort BatchId;
		[FieldOffset(6)]
		public readonly ushort RemoteBatchId;
		[FieldOffset(8)]
		public readonly long Time;

		public BatchMessage(int worldId, ushort batchId, ushort receivedBatchId, long time)
		{
			WorldId = worldId;
			BatchId = batchId;
			RemoteBatchId = receivedBatchId;
			Time = time;
		}
	}
}
