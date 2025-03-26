namespace ZeroGdk.Client.Network
{
	internal enum NetworkErrorCodes
	{
		None = 0,
		ReceiveBufferExceeded =		0x1,
		ReceiveQueueExceeded =		0x2,
		ConnectionFailed =			0x4
	}
}
