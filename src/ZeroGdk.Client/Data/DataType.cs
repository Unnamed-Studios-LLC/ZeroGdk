using System;
using System.Linq;
using System.Reflection;

namespace ZeroGdk.Client.Data
{
	public abstract class DataType
	{
		internal DataType(byte id, Type type, DataHandler handler)
		{
			Id = id;
			Type = type;
			Size = handler.Size;
			MaxSpanLength = GetMaxSpanLength(type, Size);
			Handler = handler;
		}

		public byte Id { get; }
		public Type Type { get; }
		public int Size { get; }
		public int MaxSpanLength { get; }
		internal DataHandler Handler { get; }

		private static int GetMaxSpanLength(Type type, int size)
		{
			var attribute = type.GetCustomAttribute<MaxSpanLengthAttribute>();
			if (attribute != null)
			{
				return Math.Min(ushort.MaxValue / size, attribute.Length);
			}

			if (size == 0)
			{
				return ushort.MaxValue;
			}

			return ushort.MaxValue / size;
		}
	}

	public unsafe sealed class DataType<T> : DataType where T : unmanaged
	{
		public DataType(byte id) : base(id, typeof(T), new DataHandler<T>(GetSize(typeof(T))))
		{
		}

		private static int GetSize(Type type)
		{
			var zeroSize = type.IsValueType && !type.IsPrimitive &&
				type.GetFields((BindingFlags)0x34).All(fi => GetSize(fi.FieldType) == 0);
			return zeroSize ? 0 : sizeof(T);
		}
	}
}
