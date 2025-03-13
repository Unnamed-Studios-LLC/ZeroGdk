namespace ZeroGdk.Core
{
	public interface IDataHandler<T>
	{
		void HandleData(in T data);
	}
}
