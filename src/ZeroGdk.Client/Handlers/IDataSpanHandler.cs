using System;

namespace ZeroGdk.Client
{
	public interface IDataSpanHandler<T>
	{
		bool HandleData(in ReadOnlySpan<T> data);
	}
}
