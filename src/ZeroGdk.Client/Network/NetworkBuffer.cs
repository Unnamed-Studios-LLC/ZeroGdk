using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroGdk.Client.Network
{
	internal readonly struct NetworkBuffer
	{
		public readonly byte[] Data;
		public readonly int Count;

		public NetworkBuffer(byte[] data, int length)
		{
			Data = data;
			Count = length;
		}
	}
}
