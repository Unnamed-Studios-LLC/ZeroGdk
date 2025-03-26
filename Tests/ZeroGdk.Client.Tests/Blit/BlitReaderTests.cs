using ZeroGdk.Client.Blit;

namespace ZeroGdk.Client.Tests.Blit
{
	public unsafe class BlitReaderTests
	{
		[Fact]
		public void ReadSingleValue_Success()
		{
			// Arrange: Create a buffer containing an int (42)
			int testValue = 42;
			byte[] buffer = new byte[sizeof(int)];
			byte[] bytes = BitConverter.GetBytes(testValue);
			Array.Copy(bytes, buffer, sizeof(int));

			fixed (byte* pBuffer = buffer)
			{
				var reader = new BlitReader(pBuffer, buffer.Length);
				int result = 0;

				// Act: Read a single int value from the buffer
				bool success = reader.Read(&result);

				// Assert: The read should succeed and the value should be correct.
				Assert.True(success);
				Assert.Equal(testValue, result);
				Assert.Equal(sizeof(int), reader.BytesRead);
				Assert.False(reader.IsFaulted);
			}
		}

		[Fact]
		public void ReadArray_Success()
		{
			// Arrange: Create a buffer containing an array of ints {1, 2, 3}
			int[] testValues = new int[] { 1, 2, 3 };
			int count = testValues.Length;
			byte[] buffer = new byte[count * sizeof(int)];
			for (int i = 0; i < count; i++)
			{
				byte[] intBytes = BitConverter.GetBytes(testValues[i]);
				Array.Copy(intBytes, 0, buffer, i * sizeof(int), sizeof(int));
			}

			fixed (byte* pBuffer = buffer)
			{
				var reader = new BlitReader(pBuffer, buffer.Length);
				int[] result = new int[count];

				fixed (int* pResult = result)
				{
					// Act: Read an array of ints from the buffer.
					bool success = reader.Read(pResult, count);

					// Assert: Verify the read operation was successful.
					Assert.True(success);
				}
				Assert.Equal(testValues, result);
				Assert.Equal(count * sizeof(int), reader.BytesRead);
				Assert.False(reader.IsFaulted);
			}
		}

		[Fact]
		public void ReadBeyondCapacity_Fails()
		{
			// Arrange: Create a buffer with capacity for only one int
			int testValue = 123;
			byte[] buffer = new byte[sizeof(int)];
			byte[] bytes = BitConverter.GetBytes(testValue);
			Array.Copy(bytes, buffer, sizeof(int));

			fixed (byte* pBuffer = buffer)
			{
				var reader = new BlitReader(pBuffer, buffer.Length);
				int[] result = new int[2];

				fixed (int* pResult = result)
				{
					// Act: Attempt to read two ints from a buffer that can only hold one.
					bool success = reader.Read(pResult, 2);

					// Assert: The read should fail, no bytes should be advanced, and Faults should be set.
					Assert.False(success);
					Assert.Equal(0, reader.BytesRead);
					Assert.True(reader.IsFaulted);
				}
			}
		}

		[Fact]
		public void Seek_ResetsBytesRead()
		{
			// Arrange: Create a buffer with two ints {10, 20}
			int[] testValues = new int[] { 10, 20 };
			byte[] buffer = new byte[testValues.Length * sizeof(int)];
			for (int i = 0; i < testValues.Length; i++)
			{
				byte[] intBytes = BitConverter.GetBytes(testValues[i]);
				Array.Copy(intBytes, 0, buffer, i * sizeof(int), sizeof(int));
			}

			fixed (byte* pBuffer = buffer)
			{
				var reader = new BlitReader(pBuffer, buffer.Length);

				int value1 = 0;
				bool success1 = reader.Read(&value1);
				Assert.True(success1);
				Assert.Equal(testValues[0], value1);
				Assert.Equal(sizeof(int), reader.BytesRead);

				// Act: Seek back to the beginning of the buffer.
				reader.Seek(0);
				Assert.Equal(0, reader.BytesRead);

				int value2 = 0;
				bool success2 = reader.Read(&value2);
				// Assert: Reading again after seek should return the first value.
				Assert.True(success2);
				Assert.Equal(testValues[0], value2);
			}
		}
	}
}
