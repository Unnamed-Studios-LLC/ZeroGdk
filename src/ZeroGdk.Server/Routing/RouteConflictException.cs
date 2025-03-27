namespace ZeroGdk.Server
{
	public sealed class RouteConflictException(string path,
		Type existingType,
		Type conflictingType) : Exception($"Route conflict on path '{path}' for type '{existingType}' and '{conflictingType}'")
	{
		public string Path { get; } = path;
		public Type ExistingType { get; } = existingType;
		public Type ConflictingType { get; } = conflictingType;
	}
}
