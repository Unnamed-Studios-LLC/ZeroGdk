namespace ZeroGdk.Core.Messages
{
	internal enum MessageType : byte
	{
		Batch = 0,
		Transfer = 1,
		Ping = 2,
		Pong = 3,
		UpdateEntities = 4,
		RemoveEntities = 5
	}
}
