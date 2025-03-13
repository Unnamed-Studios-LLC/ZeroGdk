using System.Runtime.InteropServices;

namespace ZeroGdk.Core.Messages
{
	[StructLayout(LayoutKind.Explicit, Size = 14)]
	internal readonly struct BatchMessage
	{
		[FieldOffset(0)]
		public readonly uint WorldId;
		[FieldOffset(4)]
		public readonly ushort BatchId;
		[FieldOffset(6)]
		public readonly long Time;

		public BatchMessage(uint worldId, ushort batchId, long time)
		{
			WorldId = worldId;
			BatchId = batchId;
			Time = time;
		}
	}
}
