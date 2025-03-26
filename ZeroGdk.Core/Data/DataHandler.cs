using System;
using ZeroGdk.Core.Blit;

namespace ZeroGdk.Core.Data
{
	internal abstract class DataHandler
	{
		public DataHandler(int size)
		{
			Size = size;
		}

		public int Size { get; }

		public abstract bool HandleData(ref BlitReader reader, object dataHandler);
		public abstract bool HandleDataSpan(ref BlitReader reader, object dataHandler, int length);
		public abstract bool HandleRawData(ref RawBlitReader reader, object dataHandler);
		public abstract bool HandleRawDataSpan(ref RawBlitReader reader, object dataHandler, int length);
	}

	internal unsafe sealed class DataHandler<T> : DataHandler where T : unmanaged
	{
		public DataHandler(int size) : base(size)
		{
		}

		public override bool HandleData(ref BlitReader reader, object dataHandler)
		{
			T data = default;
			if (Size != 0 &&
				!reader.Read(&data))
			{
				return false;
			}

			if (dataHandler is IDataHandler<T> typedHandler)
			{
				return typedHandler.HandleData(in data);
			}
			return true;
		}

		public override bool HandleDataSpan(ref BlitReader reader, object dataHandler, int length)
		{
			T* data = stackalloc T[length];
			if (Size != 0 &&
				!reader.Read(data, length))
			{
				return false;
			}

			if (dataHandler is IDataSpanHandler<T> typedHandler)
			{
				return typedHandler.HandleData(new ReadOnlySpan<T>(data, length));
			}
			return true;
		}

		public override bool HandleRawData(ref RawBlitReader reader, object dataHandler)
		{
			T* data = default;
			if (Size != 0 &&
				!reader.Read(&data))
			{
				return false;
			}

			if (dataHandler is IDataHandler<T> typedHandler)
			{
				return typedHandler.HandleData(in *data);
			}
			return true;
		}

		public override bool HandleRawDataSpan(ref RawBlitReader reader, object dataHandler, int length)
		{
			T* data = default;
			var readLength = Size == 0 ? 0 : length;
			if (!reader.Read(&data, readLength))
			{
				return false;
			}

			if (dataHandler is IDataSpanHandler<T> typedHandler)
			{
				return typedHandler.HandleData(new ReadOnlySpan<T>(data, length));
			}
			return true;
		}
	}
}
