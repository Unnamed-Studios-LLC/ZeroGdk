using System;

namespace ZeroGdk.Client.Data
{
	internal sealed class EntityData
	{
		public DataWriter EventWriter;
		public DataWriter PersistentWriter;
		public DataWriter PersistentChangeWriter;

		private long _version;

		public EntityData(int entityId)
		{
			EntityId = entityId;
		}

		public int EntityId { get; set; }

		public void Clear()
		{
			_version = 0;
			EventWriter.Clear();
			PersistentWriter.Clear();
			PersistentChangeWriter.Clear();
		}

		public void ClearOneOff(long version)
		{
			if (_version == version)
			{
				return;
			}

			EventWriter.Clear();
			PersistentChangeWriter.Clear();
			_version = version;
		}

		public bool TryReadPersistent<T>(DataType<T> dataType, int[] dataSizes, out T data) where T : unmanaged
		{
			return PersistentWriter.TryRead(dataType, dataSizes, out data);
		}

		public bool TryReadPersistent<T>(DataType<T> dataType, int[] dataSizes, Span<T> dataSpan, out ushort length) where T : unmanaged
		{
			return PersistentWriter.TryRead(dataType, dataSizes, dataSpan, out length);
		}

		public void WriteEvent<T>(long version, DataType<T> dataType, in T data) where T : unmanaged
		{
			ClearOneOff(version);
			EventWriter.WriteEvent(dataType, in data);
		}

		public void WriteEvent<T>(long version, DataType<T> dataType, in ReadOnlySpan<T> data) where T : unmanaged
		{
			ClearOneOff(version);
			EventWriter.WriteEvent(dataType, in data);
		}

		public void WritePersistentChange<T>(long version, DataType<T> dataType, in T data, int[] dataSizes) where T : unmanaged
		{
			ClearOneOff(version);
			PersistentWriter.WritePersistent(dataType, in data, dataSizes);
			PersistentChangeWriter.WritePersistent(dataType, in data, dataSizes);
		}

		public void WritePersistentChange<T>(long version, DataType<T> dataType, in ReadOnlySpan<T> data, int[] dataSizes) where T : unmanaged
		{
			ClearOneOff(version);
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
