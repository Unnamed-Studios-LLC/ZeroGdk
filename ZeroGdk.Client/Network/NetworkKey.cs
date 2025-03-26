using System.Runtime.InteropServices;

namespace ZeroGdk.Client.Network
{
	[StructLayout(LayoutKind.Explicit, Size = Length)]
	internal unsafe struct NetworkKey
	{
		public const int Length = 32;

		[FieldOffset(0)]
		public fixed byte Data[Length];
	}
}
