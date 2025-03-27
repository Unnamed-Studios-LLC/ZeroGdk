using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ZeroGdk.Client.Data;

namespace ZeroGdk.Client
{
	public sealed class DataBuilder
	{
		public const int MaxDatas = byte.MaxValue - 1;

		private readonly List<DataType> _dataTypes = new List<DataType>();
		private readonly HashSet<Type> _types = new HashSet<Type>();

		/// <summary>
		/// Registers a new data type to be included in the data encoding system.  
		/// A maximum of <see cref="MaxDatas"/> unique types can be added.  
		/// The data type must be unmanaged and decorated with a <see cref="StructLayoutAttribute"/> using <see cref="LayoutKind.Explicit"/>.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to register.</typeparam>
		/// <returns>The current <see cref="DataBuilder"/> instance for chaining.</returns>
		/// <exception cref="DataException">
		/// Thrown if:
		/// - The type has already been added.
		/// - The maximum number of allowed types has been reached.
		/// - The type is missing a <see cref="StructLayoutAttribute"/>.
		/// - The layout kind is not <see cref="LayoutKind.Explicit"/>.
		/// </exception>
		public DataBuilder Add<T>() where T : unmanaged
		{
			var type = typeof(T);
			if (!_types.Add(type))
			{
				throw new DataException(type, DataExceptionType.TypeAlreadyAdded, $"Data type '{type}' has already been added.");
			}

			if (_dataTypes.Count >= MaxDatas)
			{
				throw new DataException(type, DataExceptionType.MaxTypesReached, $"Cannot add '{type}' a maximum of {MaxDatas} has been reached.");
			}

			if (type.StructLayoutAttribute == null)
			{
				throw new DataException(type, DataExceptionType.MissingStructLayout, $"Data type '{type}' is missing {nameof(StructLayoutAttribute)}");
			}

			if (type.StructLayoutAttribute.Value != LayoutKind.Explicit)
			{
				throw new DataException(type, DataExceptionType.MissingLayoutKindExplicit, $"Data type '{type}' must have {nameof(LayoutKind)}.{nameof(LayoutKind.Explicit)} value in {nameof(StructLayoutAttribute)}");
			}

			var dataType = new DataType<T>((byte)_dataTypes.Count);
			_dataTypes.Add(dataType);
			return this;
		}

		/// <summary>
		/// Finalizes the data registration and constructs a <see cref="DataEncoding"/> instance  
		/// that can be used for serializing and identifying data types.
		/// </summary>
		/// <returns>A new <see cref="DataEncoding"/> containing all added types.</returns>
		public DataEncoding Build()
		{
			return new DataEncoding(_dataTypes);
		}
	}
}
