namespace ZeroGdk.Server
{
	public sealed class EntitiesOptions
	{
		public int ChunkSizeInBytes { get; set; } = 16_382;
		public int MinimumAmountOfEntitiesPerChunk { get; set; } = 100;
		public int ArchetypeCapacity { get; set; } = 2;
		public int EntityCapacity { get; set; } = 64;
	}
}
