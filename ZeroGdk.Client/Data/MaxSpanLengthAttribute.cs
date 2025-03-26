using System;

namespace ZeroGdk.Client
{
	/// <summary>
	/// Specifies the maximum allowed length for a data span of the associated type when used in network transmission or serialization.
	/// This attribute should be applied to <c>struct</c> types to define an upper limit on the number of elements allowed in a span of that type.
	/// </summary>
	/// <remarks>
	/// Used by the data encoding system to enforce limits on span lengths for types marked with <see cref="StructLayoutAttribute"/>.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
	public sealed class MaxSpanLengthAttribute : Attribute
	{
		/// <param name="length">The maximum number of elements permitted in the span.</param>
		public MaxSpanLengthAttribute(int length)
		{
			Length = length;
		}

		/// <summary>
		/// The maximum number of elements permitted in the span.
		/// </summary>
		public int Length { get; }
	}
}
