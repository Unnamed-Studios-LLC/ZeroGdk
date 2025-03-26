using System;

namespace ZeroGdk.Core
{
	public interface IDataSpanHandler<T>
	{
		bool HandleData(in ReadOnlySpan<T> data);
	}
}
