using System.Runtime.InteropServices;

namespace ZeroGdk.Core.Messages
{
	[StructLayout(LayoutKind.Explicit, Size = 2)]
	internal readonly struct RemovedEntitiesMessage
	{
		[FieldOffset(0)]
		public readonly ushort Count;

		public RemovedEntitiesMessage(ushort count)
		{
			Count = count;
		}
	}
}
