namespace ZeroGdk.Client
{
	public interface IDataHandler<T>
	{
		bool HandleData(in T data);
	}
}
