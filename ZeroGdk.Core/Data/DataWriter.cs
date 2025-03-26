using System;
using System.Buffers;
using ZeroGdk.Core.Blit;

namespace ZeroGdk.Core.Data
{
	internal unsafe struct DataWriter
	{
		public const byte SpanFlag = 0xff;

		private const int MinCapacity = 32;

		public byte[] Data { get; private set; }
		public int BytesWritten { get; private set; }
		public int DataWritten { get; private set; }

		public void Clear()
		{
			if (Data != null)
			{
				ArrayPool<byte>.Shared.Return(Data);
				Data = null;
			}
			BytesWritten = 0;
			DataWritten = 0;
		}

		public void WriteEvent<T>(DataType<T> dataType, in T data) where T : unmanaged
		{
			if (DataWritten == ushort.MaxValue)
			{
				throw new MaxDataException();
			}

			// calculate new size
			var newSize = BytesWritten + sizeof(byte) + dataType.Size;

			// setup or expand buffer
			EnsureData(newSize);

			// write
			fixed (byte* pBuffer = &Data[BytesWritten])
			{
				// write id
				*pBuffer = dataType.Id;

				// write data
				if (dataType.Size != 0)
				{
					*(T*)(pBuffer + 1) = data;
				}
			}
			
			BytesWritten = newSize;
			DataWritten++;
		}

		public void WriteEvent<T>(DataType<T> dataType, in ReadOnlySpan<T> data) where T : unmanaged
		{
			if (DataWritten == ushort.MaxValue)
			{
				throw new MaxDataException();
			}

			// calculate new size
			var newSize = BytesWritten + sizeof(byte) + sizeof(byte) + sizeof(ushort) + dataType.Size * data.Length;

			// setup or expand buffer
			EnsureData(newSize);

			// write
			fixed (byte* pBuffer = &Data[BytesWritten])
			{
				// write span flag
				*pBuffer = SpanFlag;

				// write id
				*(pBuffer + 1) = dataType.Id;

				// write length
				*(ushort*)(pBuffer + 2) = (ushort)data.Length;

				// write data
				if (dataType.Size != 0 &&
					data.Length > 0)
				{
					fixed (T* pData = data)
					{
						Buffer.MemoryCopy(pData, (T*)(pBuffer + 4), dataType.Size * data.Length, dataType.Size * data.Length);
					}
				}
			}

			BytesWritten = newSize;
			DataWritten++;
		}

		public void WritePersistent<T>(DataType<T> dataType, in T data, int[] dataSizes) where T : unmanaged
		{
			var newSize = BytesWritten;
			var dataPosition = PositionOf(dataType.Id, false, dataSizes, out _);
			var contains = dataPosition >= 0;

			// calculate new size
			if (!contains)
			{
				dataPosition = BytesWritten;
				newSize += sizeof(byte) + dataType.Size;
			}

			// setup or expand buffer
			EnsureData(newSize);

			// write to buffer
			fixed (byte* pBuffer = &Data[dataPosition])
			{
				// write id
				*pBuffer = dataType.Id;

				// write data
				if (dataType.Size != 0)
				{
					*(T*)(pBuffer + 1) = data;
				}
			}

			BytesWritten = newSize;
			if (!contains)
			{
				DataWritten++;
			}
		}

		public void WritePersistent<T>(DataType<T> dataType, in ReadOnlySpan<T> data, int[] dataSizes) where T : unmanaged
		{
			var newSize = BytesWritten;
			var dataPosition = PositionOf(dataType.Id, true, dataSizes, out var currentSize);
			var contains = dataPosition >= 0;

			// calculate new size
			var spanSize = sizeof(byte) + sizeof(byte) + sizeof(ushort) + dataType.Size * data.Length;
			if (!contains)
			{
				dataPosition = BytesWritten;
				newSize += spanSize;
			}
			else if (spanSize != currentSize)
			{
				newSize += spanSize - currentSize;
			}

			// setup or expand buffer
			EnsureData(newSize);

			if (spanSize != currentSize)
			{
				// span size changed, make or reduce the space it takes up
				var end = dataPosition + currentSize;
				if (end != BytesWritten)
				{
					var newEnd = dataPosition + newSize;
					var moveSize = BytesWritten - end;
					fixed (byte* pSrc = &Data[end])
					fixed (byte* pDst = &Data[newEnd])
					{
						Buffer.MemoryCopy(pSrc, pDst, moveSize, moveSize);
					}
				}
			}

			// write to buffer
			fixed (byte* pBuffer = &Data[dataPosition])
			{
				// write span flag
				*pBuffer = SpanFlag;

				// write id
				*(pBuffer + 1) = dataType.Id;

				// write length
				*(ushort*)(pBuffer + 2) = (ushort)data.Length;

				// write data
				if (dataType.Size != 0 &&
					data.Length > 0)
				{
					fixed (T* pData = data)
					{
						Buffer.MemoryCopy(pData, (T*)(pBuffer + 4), dataType.Size * data.Length, dataType.Size * data.Length);
					}
				}
			}

			BytesWritten = newSize;
			if (!contains)
			{
				DataWritten++;
			}
		}

		public bool WriteTo(ref BlitWriter writer)
		{
			if (BytesWritten == 0)
			{
				return true;
			}

			fixed (byte* pSrc = Data)
			{
				return writer.Write(pSrc, BytesWritten);
			}
		}

		private void EnsureData(int newSize)
		{
			if (Data == null)
			{
				Data = ArrayPool<byte>.Shared.Rent(MinCapacity);
			}
			else if (newSize > Data.Length)
			{
				var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
				fixed (byte* pSrc = Data)
				fixed (byte* pDst = newBuffer)
				{
					Buffer.MemoryCopy(pSrc, pDst, newSize, BytesWritten);
				}
				ArrayPool<byte>.Shared.Return(Data);
				Data = newBuffer;
			}
		}

		private int PositionOf(byte dataType, bool span, int[] dataSizes, out int size)
		{
			if (Data == null)
			{
				size = 0;
				return -1;
			}

			int offset = 0;
			for (int i = 0; i < DataWritten; i++)
			{
				var recordType = Data[offset];
				var isSpan = recordType == SpanFlag;
				if (isSpan)
				{
					recordType = Data[offset + 1];
					size = sizeof(byte) + sizeof(byte) + sizeof(ushort) + dataSizes[recordType] * BitConverter.ToUInt16(Data, offset + 2);
				}
				else
				{
					size = sizeof(byte) + dataSizes[recordType];
				}
				
				if (recordType == dataType &&
					span == isSpan)
				{
					return offset;
				}

				offset += size;
			}

			size = 0;
			return -1;
		}
	}
}
