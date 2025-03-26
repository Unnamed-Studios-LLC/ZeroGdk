using System;
using System.Runtime.CompilerServices;

namespace ZeroGdk.Client.Blit
{
    internal unsafe struct BlitReader
    {
        private readonly byte* _buffer;
        private int _count;

        public BlitReader(byte* buffer, int capacity)
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

        public bool Read<T>(T* destination, int length) where T : unmanaged
        {
            if (!CanContinue(sizeof(T) * length))
            {
                return false;
            }

            var sourcePointer = (T*)(_buffer + _count);
            Buffer.MemoryCopy(sourcePointer, destination, sizeof(T) * length, sizeof(T) * length);
            _count += sizeof(T) * length;

            if (!BitConverter.IsLittleEndian && sizeof(T) != 1)
            {
                for (int i = 0; i < length; i++)
                {
                    EndianBlit<T>.SwapBytes((byte*)(destination + i));
                }
            }

            return true;
        }

        public bool Read<T>(T* value) where T : unmanaged
        {
            if (!CanContinue(sizeof(T)))
            {
                return false;
            }

            var pntr = (T*)(_buffer + _count);
            *value = *pntr;
            _count += sizeof(T);

            if (!BitConverter.IsLittleEndian && sizeof(T) != 1)
            {
                EndianBlit<T>.SwapBytes((byte*)value);
            }

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
