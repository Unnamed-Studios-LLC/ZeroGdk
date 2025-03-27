using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroGdk.Client.Blit
{
	internal unsafe static class EndianBlit
    {
        public unsafe delegate void SwapDelegate(byte* pointer);

        private static readonly Type s_fixedBufferAttributeType = typeof(FixedBufferAttribute);

        public static SwapDelegate GenerateSwapElement(Type elementType)
        {
            if (elementType.IsEnum)
            {
                elementType = Enum.GetUnderlyingType(elementType);
            }

            if (elementType.IsPointer)
            {
                switch (IntPtr.Size)
                {
                    case 8:
                        return Swap8;
                    case 4:
                        return Swap4;
                    case 2: // idk who uses Int16 pointers, but might as well have it
                        return Swap2;
                    default:
                        return null;
                }
            }

            var typeCode = Type.GetTypeCode(elementType);
            if (elementType.IsPrimitive ||
                typeCode == TypeCode.Decimal)
            {
                switch (typeCode)
                {
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Char:
                        return Swap2;
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Single:
                        return Swap4;
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Double:
                        return Swap8;
                    case TypeCode.Decimal:
                        return Swap16;
                    default:
                        return null;
                }
            }

            // else this is a nested struct
            return GenerateSwapStruct(elementType);
        }

        private static SwapDelegate GenerateSwapField(FieldInfo field)
        {
            var attributes = field.GetCustomAttributes(s_fixedBufferAttributeType, false);
            if (attributes.Length > 0)
            {
                var list = new List<SwapDelegate>();
                var attribute = (FixedBufferAttribute)attributes[0];
                for (int i = 0; i < attribute.Length; i++)
                {
                    list.Add(GenerateSwapElement(attribute.ElementType));
                }
                var elementSize = Marshal.SizeOf(attribute.ElementType);

                void swapFixedBuffer(byte* pointer)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        list[i].Invoke(pointer + i * elementSize);
                    }
                }
                return swapFixedBuffer;
            }
            else
            {
                return GenerateSwapElement(field.FieldType);
            }
        }

        private static SwapDelegate GenerateSwapStruct(Type type)
        {
            var list = new List<SwapDelegate>();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(x => (GetFieldOffset(x), x))
                .OrderBy(x => x.Item1);

            foreach (var (offset, field) in fields)
            {
                var swap = GenerateSwapField(field);
                if (swap == null)
                {
                    continue;
                }

                void @delegate(byte* pointer)
                {
                    swap(pointer + offset);
                }
                list.Add(@delegate);
            }

            void swapDelegate(byte* pointer)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].Invoke(pointer);
                }
            }
            return swapDelegate;
        }

        /// <summary>
        /// from https://stackoverflow.com/questions/30817924/obtain-non-explicit-field-offset
        /// </summary>
        /// <param name="fi"></param>
        /// <returns></returns>
        private static int GetFieldOffset(FieldInfo fi) => GetFieldOffset(fi.FieldHandle);
        private static int GetFieldOffset(RuntimeFieldHandle h) => Marshal.ReadInt32(h.Value + (4 + IntPtr.Size)) & 0xFFFFFF;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(byte* a, byte* b)
        {
            var temp = *a;
            *a = *b;
            *b = temp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap2(byte* pointer)
        {
            Swap(pointer + 0, pointer + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap4(byte* pointer)
        {
            Swap(pointer + 0, pointer + 3);
            Swap(pointer + 1, pointer + 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap8(byte* pointer)
        {
            Swap(pointer + 0, pointer + 7);
            Swap(pointer + 1, pointer + 6);
            Swap(pointer + 2, pointer + 5);
            Swap(pointer + 3, pointer + 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap16(byte* pointer)
        {
            Swap(pointer + 0, pointer + 15);
            Swap(pointer + 1, pointer + 14);
            Swap(pointer + 2, pointer + 13);
            Swap(pointer + 3, pointer + 12);
            Swap(pointer + 4, pointer + 11);
            Swap(pointer + 5, pointer + 10);
            Swap(pointer + 6, pointer + 9);
            Swap(pointer + 7, pointer + 8);
        }
    }

    internal unsafe static class EndianBlit<T> where T : unmanaged
    {
        private static readonly EndianBlit.SwapDelegate Swap = EndianBlit.GenerateSwapElement(typeof(T));

        public static void SwapBytes(byte* pointer)
        {
            Swap.Invoke(pointer);
        }
    }
}
