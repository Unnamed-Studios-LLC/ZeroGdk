using System.Runtime.InteropServices;
using ZeroGdk.Core.Network;

namespace ZeroGdk.Core.Messages
{
	[StructLayout(LayoutKind.Explicit, Size = 10 + NetworkKey.Length)]
	internal readonly struct TransferMessage
	{
		[FieldOffset(0)]
		public readonly long Ip;
		[FieldOffset(8)]
		public readonly ushort Port;
		[FieldOffset(10)]
		public readonly NetworkKey Key;
	}
}
