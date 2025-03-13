using ZeroGdk.Server;

namespace ZeroGdk.Demo.Factories
{
	[Route("static")]
	public class StaticWorldFactory : WorldFactory<StaticWorldData>
	{
		public override Task<bool> CreateAsync(World world, StaticWorldData? data, CancellationToken cancellationToken)
		{
			return base.CreateAsync(world, data, cancellationToken);
		}

		public override Task DestroyAsync(World world, CancellationToken cancellationToken)
		{
			return base.DestroyAsync(world, cancellationToken);
		}
	}
}
