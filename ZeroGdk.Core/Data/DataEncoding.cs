using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroGdk.Core.Data
{
	public sealed class DataEncoding
	{
		private readonly DataType[] _types;
		private readonly Dictionary<Type, DataType> _encodingMap;

		internal DataEncoding(IEnumerable<DataType> types)
		{
			_types = types.ToArray();
			Sizes = new int[_types.Length];
			_encodingMap = _types.ToDictionary(x => x.Type);

			for (int i = 0; i < _types.Length; i++)
			{
				var type = _types[i];
				Sizes[i] = type.Size;
			}
		}

		public int[] Sizes { get; private set; }

		/// <summary>
		/// Attempts to retrieve a <see cref="DataType"/> by its indexed ID.
		/// </summary>
		/// <param name="id">The <see cref="byte"/> ID associated with the <see cref="DataType"/>.</param>
		/// <param name="dataType">When this method returns, contains the <see cref="DataType"/> if found; otherwise, <c>null</c>.</param>
		/// <returns><c>true</c> if a <see cref="DataType"/> was found at the specified <paramref name="id"/>; otherwise, <c>false</c>.</returns>
		public bool TryGetType(byte id, out DataType dataType)
		{
			if (id >= _types.Length)
			{
				dataType = null;
				return false;
			}

			dataType = _types[id];
			return true;
		}

		/// <summary>
		/// Attempts to retrieve a <see cref="DataType"/> based on its associated <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The runtime <see cref="Type"/> of the data type to retrieve.</param>
		/// <param name="dataType">When this method returns, contains the <see cref="DataType"/> if found; otherwise, <c>null</c>.</param>
		/// <returns><c>true</c> if a <see cref="DataType"/> was found for the specified <paramref name="type"/>; otherwise, <c>false</c>.</returns>
		public bool TryGetType(Type type, out DataType dataType)
		{
			return _encodingMap.TryGetValue(type, out dataType);
		}

		/// <summary>
		/// Attempts to retrieve the strongly typed <see cref="DataType{T}"/> for a given unmanaged type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The unmanaged type associated with the <see cref="DataType"/>.</typeparam>
		/// <param name="dataType">When this method returns, contains the <see cref="DataType{T}"/> if found; otherwise, <c>null</c>.</param>
		/// <returns><c>true</c> if a <see cref="DataType{T}"/> was found for type <typeparamref name="T"/>; otherwise, <c>false</c>.</returns>
		public bool TryGetType<T>(out DataType<T> dataType) where T : unmanaged
		{
			if (_encodingMap.TryGetValue(typeof(T), out var dType))
			{
				dataType = (DataType<T>)dType;
				return true;
			}

			dataType = null;
			return false;
		}
	}
}
