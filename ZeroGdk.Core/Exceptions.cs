using System;
using ZeroGdk.Core.Data;

namespace ZeroGdk.Core
{
	public enum DataExceptionType
	{
		Unknown = 0,
		TypeAlreadyAdded = 1,
		MaxTypesReached = 2,
		MissingStructLayout = 3,
		MissingLayoutKindExplicit = 4
	}

	public sealed class DataException : Exception
	{
		public DataException(Type dataType, DataExceptionType exceptionType, string message) : base(message)
		{
			DataType = dataType;
			ExceptionType = exceptionType;
		}

		public Type DataType { get; }
		public DataExceptionType ExceptionType { get; }
	}

	public sealed class DataNotRegisteredException : Exception
	{
		public DataNotRegisteredException(Type dataType) : base($"Unregistered data type encountered: {dataType}")
		{
			DataType = dataType;
		}

		public Type DataType { get; }
	}

	public sealed class DataSpanLengthException : Exception
	{
		public DataSpanLengthException(Type dataType, int maxSpanLength) : base($"{dataType} span length exceeds max span length: {maxSpanLength}")
		{
			DataType = dataType;
			MaxSpanLength = maxSpanLength;
		}

		public Type DataType { get; }
		public int MaxSpanLength { get; }
	}

	public sealed class MaxDataException : Exception
	{
		public MaxDataException() : base($"A max of {ushort.MaxValue} datas has been reached for this entity")
		{
		}
	}
}
