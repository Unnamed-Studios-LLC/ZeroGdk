namespace ZeroGdk.Core
{
	public interface IDataHandler<T>
	{
		bool HandleData(in T data);
	}
}
