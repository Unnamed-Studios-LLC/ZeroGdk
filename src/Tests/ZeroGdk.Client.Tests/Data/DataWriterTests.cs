using System;
using System.Buffers;
using System.Reflection;
using System.Text;
using Xunit;
using ZeroGdk.Client.Blit;
using ZeroGdk.Client.Data;

namespace ZeroGdk.Client.Tests.Data
{
	public unsafe class DataWriterTests
	{
		// Custom unmanaged structs used for testing.
		public struct TestStruct
		{
			public int Value;
		}

		public struct TestStruct2
		{
			public float X;
			public float Y;
		}

		[Fact]
		public void Clear_ResetsState()
		{
			var writer = new DataWriter();
			var dataType = new DataType<TestStruct>(1);
			var testData = new TestStruct { Value = 42 };
			writer.WriteEvent(dataType, in testData);

			// Verify that data has been written.
			Assert.NotNull(writer.Data);
			Assert.True(writer.BytesWritten > 0);
			Assert.True(writer.DataWritten > 0);

			// Now clear the writer.
			writer.Clear();

			Assert.Null(writer.Data);
			Assert.Equal(0, writer.BytesWritten);
			Assert.Equal(0, writer.DataWritten);
		}

		[Fact]
		public void WriteEvent_Scalar_WritesCorrectly()
		{
			var writer = new DataWriter();
			var dataType = new DataType<TestStruct>(10);
			var testData = new TestStruct { Value = 12345 };
			writer.WriteEvent(dataType, in testData);

			// A scalar event writes 1 byte for the id plus the data (sizeof(int) in this case).
			int expectedSize = sizeof(byte) + sizeof(int);
			Assert.Equal(expectedSize, writer.BytesWritten);

			// Check that the first byte is the id.
			Assert.Equal(dataType.Id, writer.Data[0]);

			// Verify that the int data was written correctly.
			int valueFromBuffer = BitConverter.ToInt32(writer.Data, 1);
			Assert.Equal(testData.Value, valueFromBuffer);
		}

		[Fact]
		public void WriteEvent_Span_WritesCorrectly()
		{
			var writer = new DataWriter();
			var dataType = new DataType<TestStruct>(20);
			var testDataArray = new TestStruct[]
			{
				new TestStruct { Value = 1 },
				new TestStruct { Value = 2 },
				new TestStruct { Value = 3 },
			};
			ReadOnlySpan<TestStruct> span = testDataArray;
			writer.WriteEvent(dataType, span);

			// A span event writes:
			// - 1 byte: span flag (0xff)
			// - 1 byte: id
			// - 2 bytes: length (ushort)
			// - followed by the data (dataType.Size * number of elements)
			int expectedSize = sizeof(byte) + sizeof(byte) + sizeof(ushort) + (sizeof(int) * testDataArray.Length);
			Assert.Equal(expectedSize, writer.BytesWritten);

			// Check span flag.
			Assert.Equal(0xff, writer.Data[0]);
			// Check the id.
			Assert.Equal(dataType.Id, writer.Data[1]);
			// Check the length.
			ushort length = BitConverter.ToUInt16(writer.Data, 2);
			Assert.Equal((ushort)testDataArray.Length, length);
			// Check the first element's data.
			int firstValue = BitConverter.ToInt32(writer.Data, 4);
			Assert.Equal(testDataArray[0].Value, firstValue);
		}

		[Fact]
		public void WritePersistent_Scalar_AppendsOrUpdatesCorrectly()
		{
			var writer = new DataWriter();
			var dataType = new DataType<TestStruct>(30);
			// Create a simple lookup for data sizes.
			int[] dataSizes = new int[256];
			dataSizes[dataType.Id] = dataType.Size;

			// Write the persistent event for the first time.
			var testData1 = new TestStruct { Value = 100 };
			writer.WritePersistent(dataType, in testData1, dataSizes);
			int initialBytes = writer.BytesWritten;
			int initialDataWritten = writer.DataWritten;

			// Write again with a different value.
			var testData2 = new TestStruct { Value = 200 };
			writer.WritePersistent(dataType, in testData2, dataSizes);

			// Since the event is persistent, DataWritten should not increase.
			Assert.Equal(initialDataWritten, writer.DataWritten);
			// And BytesWritten should remain unchanged (the record is updated in place).
			Assert.Equal(initialBytes, writer.BytesWritten);

			// Verify that the new value has been written.
			// For scalar events, the record is stored as: [id][data].
			int offset = initialBytes - (sizeof(byte) + sizeof(int));
			int valueFromBuffer = BitConverter.ToInt32(writer.Data, offset + 1);
			Assert.Equal(testData2.Value, valueFromBuffer);
		}

		[Fact]
		public void WritePersistent_Span_AppendsOrUpdatesCorrectly()
		{
			var writer = new DataWriter();
			var dataType = new DataType<TestStruct>(40);
			int[] dataSizes = new int[256];
			dataSizes[dataType.Id] = dataType.Size;

			// Write a persistent span event the first time.
			var testDataArray1 = new TestStruct[]
			{
				new TestStruct { Value = 10 },
				new TestStruct { Value = 20 },
			};
			writer.WritePersistent(dataType, new ReadOnlySpan<TestStruct>(testDataArray1), dataSizes);
			int initialBytes = writer.BytesWritten;
			int initialDataWritten = writer.DataWritten;

			// Write again with a different array (different length).
			var testDataArray2 = new TestStruct[]
			{
				new TestStruct { Value = 30 },
				new TestStruct { Value = 40 },
				new TestStruct { Value = 50 },
			};
			writer.WritePersistent(dataType, new ReadOnlySpan<TestStruct>(testDataArray2), dataSizes);

			// Persistent write should not increase DataWritten.
			Assert.Equal(initialDataWritten, writer.DataWritten);
			// BytesWritten should update if the span length changed.
			int expectedNewSize = sizeof(byte) + sizeof(byte) + sizeof(ushort) + (dataType.Size * testDataArray2.Length);
			// (In this test we expect the overall BytesWritten to now equal the updated persistent record size.)
			Assert.Equal(expectedNewSize, writer.BytesWritten);

			// Check that the record begins with the span flag.
			Assert.Equal(0xff, writer.Data[0]);
			// Check the id.
			Assert.Equal(dataType.Id, writer.Data[1]);
			// Check the length field.
			ushort length = BitConverter.ToUInt16(writer.Data, 2);
			Assert.Equal((ushort)testDataArray2.Length, length);
			// Verify the first element’s data.
			int firstValue = BitConverter.ToInt32(writer.Data, 4);
			Assert.Equal(testDataArray2[0].Value, firstValue);
		}

		[Fact]
		public unsafe void WriteTo_WritesDataCorrectly()
		{
			var writer = new DataWriter();
			var dataType = new DataType<TestStruct>(50);
			var testData = new TestStruct { Value = 999 };
			writer.WriteEvent(dataType, in testData);

			// Allocate a managed buffer large enough to hold the written data.
			byte[] buffer = new byte[writer.BytesWritten];
			fixed (byte* pBuffer = buffer)
			{
				// Create a BlitWriter using the actual implementation.
				var blitWriter = new BlitWriter(pBuffer, buffer.Length);
				bool result = writer.WriteTo(ref blitWriter);
				Assert.True(result);
				Assert.Equal(writer.BytesWritten, blitWriter.BytesWritten);
			}

			// Verify that the contents of the buffer match the DataWriter's data.
			for (int i = 0; i < writer.BytesWritten; i++)
			{
				Assert.Equal(writer.Data[i], buffer[i]);
			}
		}

		[Fact]
		public void WriteEvent_Scalar_ThrowsMaxDataException_WhenDataWrittenLimitReached()
		{
			var writer = new DataWriter();
			// Use reflection to simulate that DataWritten has reached ushort.MaxValue.
			var field = typeof(DataWriter).GetField("<DataWritten>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(field);
			object boxed = writer;
			field.SetValue(boxed, ushort.MaxValue);
			writer = (DataWriter)boxed;

			var dataType = new DataType<TestStruct>(60);
			var testData = new TestStruct { Value = 123 };

			Assert.Throws<MaxDataException>(() => writer.WriteEvent(dataType, in testData));
		}

		[Fact]
		public void WriteEvent_Span_ThrowsMaxDataException_WhenDataWrittenLimitReached()
		{
			var writer = new DataWriter();
			var field = typeof(DataWriter).GetField("<DataWritten>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(field);
			object boxed = writer;
			field.SetValue(boxed, ushort.MaxValue);
			writer = (DataWriter)boxed;

			var dataType = new DataType<TestStruct>(70);
			var testDataArray = new TestStruct[] { new TestStruct { Value = 321 } };

			Assert.Throws<MaxDataException>(() => writer.WriteEvent(dataType, new ReadOnlySpan<TestStruct>(testDataArray)));
		}
	}
}
