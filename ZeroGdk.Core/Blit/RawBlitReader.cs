using System;
using System.Runtime.CompilerServices;

namespace ZeroGdk.Core.Blit
{
    internal unsafe struct RawBlitReader
    {
        private readonly byte* _buffer;
        private int _count;

        public RawBlitReader(byte* buffer, int capacity)
        {
            _buffer = buffer;
            Capacity = capacity;
            _count = 0;
            Faults = BlitFaultCodes.None;
        }

        public int BytesRead => _count;
        public int Capacity { get; }
        public BlitFaultCodes Faults { get; private set; }
        public bool IsFaulted => Faults != BlitFaultCodes.None;

        public bool Read<T>(T** destinationPointer, int length) where T : unmanaged
        {
            if (!CanContinue(sizeof(T) * length))
            {
                return false;
            }

            *destinationPointer = (T*)(_buffer + _count);
            _count += sizeof(T) * length;

            return true;
        }

        public bool Read<T>(T** valuePointer) where T : unmanaged
        {
            if (!CanContinue(sizeof(T)))
            {
                return false;
            }

            *valuePointer = (T*)(_buffer + _count);
            _count += sizeof(T);
            return true;
		}

		public bool Read<T>(out T value) where T : unmanaged
		{
			if (!CanContinue(sizeof(T)))
			{
                value = default;
				return false;
			}

			value = *(T*)(_buffer + _count);
			_count += sizeof(T);
			return true;
		}

		public void Seek(int offset)
        {
            _count = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanContinue(int length)
        {
            Faults |= (_count + length) > Capacity ? BlitFaultCodes.CapacityExceeded : BlitFaultCodes.None;
            return Faults == BlitFaultCodes.None;
        }
    }
}
