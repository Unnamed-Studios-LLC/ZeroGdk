using System;
using Xunit;
using ZeroGdk.Client.Blit;

namespace ZeroGdk.Client.Blit.Tests
{
	public unsafe class BlitWriterTests
	{
		[Fact]
		public void WriteSingleValue_Success()
		{
			// Arrange: Create a buffer with enough space for one int.
			int testValue = 42;
			byte[] buffer = new byte[sizeof(int)];
			fixed (byte* pBuffer = buffer)
			{
				var writer = new BlitWriter(pBuffer, buffer.Length);

				// Act: Write a single int value using the "in T value" overload.
				bool success = writer.Write(testValue);

				// Assert: Writing should succeed, the number of bytes written is correct,
				// and the buffer contains the expected value.
				Assert.True(success);
				Assert.Equal(sizeof(int), writer.BytesWritten);
				Assert.False(writer.IsFaulted);

				int writtenValue = *(int*)pBuffer;
				Assert.Equal(testValue, writtenValue);
			}
		}

		[Fact]
		public void WriteArray_Success()
		{
			// Arrange: Create an array of ints to write.
			int[] testValues = new int[] { 1, 2, 3 };
			int count = testValues.Length;
			byte[] buffer = new byte[count * sizeof(int)];
			fixed (byte* pBuffer = buffer)
			{
				var writer = new BlitWriter(pBuffer, buffer.Length);
				fixed (int* pSource = testValues)
				{
					// Act: Write the array using the pointer-based overload.
					bool success = writer.Write(pSource, count);

					// Assert: Ensure the write succeeded.
					Assert.True(success);
					Assert.Equal(count * sizeof(int), writer.BytesWritten);
					Assert.False(writer.IsFaulted);
				}

				// Verify the written data by copying from the buffer.
				int[] result = new int[count];
				Buffer.BlockCopy(buffer, 0, result, 0, buffer.Length);
				Assert.Equal(testValues, result);
			}
		}

		[Fact]
		public void WriteBeyondCapacity_Fails()
		{
			// Arrange: Create a buffer that can hold only one int.
			int testValue = 123;
			byte[] buffer = new byte[sizeof(int)];
			fixed (byte* pBuffer = buffer)
			{
				var writer = new BlitWriter(pBuffer, buffer.Length);
				int[] values = new int[] { testValue, testValue };
				fixed (int* pValues = values)
				{
					// Act: Attempt to write two ints into a buffer that only has space for one.
					bool success = writer.Write(pValues, 2);

					// Assert: The write should fail, no bytes should be written, and a fault should be recorded.
					Assert.False(success);
					Assert.Equal(0, writer.BytesWritten);
					Assert.True(writer.IsFaulted);
				}
			}
		}

		[Fact]
		public void Seek_ResetsBytesWritten()
		{
			// Arrange: Create a buffer with room for two ints.
			int[] testValues = new int[] { 10, 20 };
			byte[] buffer = new byte[testValues.Length * sizeof(int)];
			fixed (byte* pBuffer = buffer)
			{
				var writer = new BlitWriter(pBuffer, buffer.Length);

				// Write the first value.
				bool success1 = writer.Write(testValues[0]);
				Assert.True(success1);
				Assert.Equal(sizeof(int), writer.BytesWritten);

				// Act: Reset the writer position to the start.
				writer.Seek(0);
				Assert.Equal(0, writer.BytesWritten);

				// Write the second value (which will overwrite the first).
				bool success2 = writer.Write(testValues[1]);
				Assert.True(success2);
				Assert.Equal(sizeof(int), writer.BytesWritten);

				// Assert: The buffer should now contain the second value.
				int writtenValue = *(int*)pBuffer;
				Assert.Equal(testValues[1], writtenValue);
			}
		}
	}
}
