using System;

namespace ZeroGdk.Core
{
	public interface IDataSpanHandler<T>
	{
		void HandleData(in ReadOnlySpan<T> data);
	}
}
