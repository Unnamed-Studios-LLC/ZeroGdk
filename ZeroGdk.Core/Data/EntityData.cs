using System;

namespace ZeroGdk.Core.Data
{
	internal sealed class EntityData
	{
		public DataWriter EventWriter;
		public DataWriter PersistentWriter;
		public DataWriter PersistentChangeWriter;
		public long Tick;

		public void ClearOneOff(long tick)
		{
			if (Tick == tick)
			{
				return;
			}

			EventWriter.Clear();
			PersistentChangeWriter.Clear();
			Tick = tick;
		}

		public void WriteEvent<T>(long tick, DataType<T> dataType, in T data) where T : unmanaged
		{
			ClearOneOff(tick);
			EventWriter.WriteEvent(dataType, in data);
		}

		public void WriteEvent<T>(long tick, DataType<T> dataType, in ReadOnlySpan<T> data) where T : unmanaged
		{
			ClearOneOff(tick);
			EventWriter.WriteEvent(dataType, in data);
		}

		public void WritePersistentChange<T>(long tick, DataType<T> dataType, in T data, int[] dataSizes) where T : unmanaged
		{
			ClearOneOff(tick);
			PersistentWriter.WritePersistent(dataType, in data, dataSizes);
			PersistentChangeWriter.WritePersistent(dataType, in data, dataSizes);
		}

		public void WritePersistentChange<T>(long tick, DataType<T> dataType, in ReadOnlySpan<T> data, int[] dataSizes) where T : unmanaged
		{
			ClearOneOff(tick);
			PersistentWriter.WritePersistent(dataType, in data, dataSizes);
			PersistentChangeWriter.WritePersistent(dataType, in data, dataSizes);
		}

		public void WritePersistentOnly<T>(DataType<T> dataType, in T data, int[] dataSizes) where T : unmanaged
		{
			PersistentWriter.WritePersistent(dataType, in data, dataSizes);
		}

		public void WritePersistentOnly<T>(DataType<T> dataType, in ReadOnlySpan<T> data, int[] dataSizes) where T : unmanaged
		{
			PersistentWriter.WritePersistent(dataType, in data, dataSizes);
		}
	}
}
