using System.Runtime.InteropServices;

namespace ZeroGdk.Core.Messages
{
	[StructLayout(LayoutKind.Explicit, Size = 2)]
	internal readonly struct UpdateEntitiesMessage
	{
		[FieldOffset(0)]
		public readonly ushort Count;

		public UpdateEntitiesMessage(ushort count)
		{
			Count = count;
		}
	}
}
