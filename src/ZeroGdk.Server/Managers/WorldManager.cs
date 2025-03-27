using Arch.Core;
using Arch.Core.External;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using ZeroGdk.Client.Data;

namespace ZeroGdk.Server
{
	internal sealed class WorldManager
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly List<World> _worldList = [];
		private readonly object _worldLock = new();
		private readonly Dictionary<int, World> _worldMap = [];

		public WorldManager(IServiceProvider serviceProvider,
			ExternalOptions externalOptions)
		{
			_serviceProvider = serviceProvider;
			Entities.ExternalOptions = externalOptions;
		}

		/// <summary>
		/// Constructs a new <see cref="World"/> using root services
		/// </summary>
		/// <returns>Newly created <see cref="World"/></returns>
		public World CreateWorld(int worldId, string factoryRoute)
		{
			return new World(
				worldId,
				factoryRoute,
				_serviceProvider,
				_serviceProvider.GetRequiredService<ILogger<World>>(),
				_serviceProvider.GetRequiredService<IOptions<EntitiesOptions>>(),
				_serviceProvider.GetRequiredService<DataEncoding>()
			);
		}

		/// <summary>
		/// Gets a <see cref="World"/> by ID
		/// </summary>
		/// <param name="worldId">ID of the <see cref="World"/> to get</param>
		/// <returns><see cref="World"/> with the ID of <paramref name="worldId"/></returns>
		public World? GetWorld(int worldId)
		{
			lock (_worldLock)
			{
				return _worldMap.TryGetValue(worldId, out var world) ? world : null;
			}
		}

		/// <summary>
		/// Removes all worlds 
		/// </summary>
		/// <param name="receiveList"></param>
		public void RemoveAllWorlds(List<World> receiveList)
		{
			lock (_worldList)
			{
				_worldMap.Clear();
				receiveList.AddRange(_worldList);
				_worldList.Clear();
			}
		}

		/// <summary>
		/// Removes a <see cref="World"/> with the ID of <paramref name="worldId"/>
		/// </summary>
		/// <param name="worldId">ID of the <see cref="World"/> to remove</param>
		/// <returns><see cref="World"/> removed by ID, null if no world found</returns>
		public World? RemoveWorld(int worldId)
		{
			lock (_worldLock)
			{
				if (!_worldMap.Remove(worldId, out var world))
				{
					return null;
				}
				_worldList.Remove(world);
				return world;
			}
		}

		public void Start()
		{

		}

		public void Stop()
		{

		}

		/// <summary>
		/// Trys to adds a <see cref="World"/>
		/// </summary>
		/// <param name="world"><see cref="World"/> to try and add</param>
		/// <returns>If the world was added</returns>
		public bool TryAddWorld(World world)
		{
			lock (_worldLock)
			{
				if (!_worldMap.TryAdd(world.WorldId, world))
				{
					return false;
				}
				_worldList.Add(world);
				return true;
			}
		}

		/// <summary>
		/// Tries to get a world with ID of <paramref name="worldId"/>, setting <paramref name="world"/> if found
		/// </summary>
		/// <param name="worldId">ID of the <see cref="World"/> to get</param>
		/// <param name="world"><see cref="World"/> with the given ID, if found</param>
		/// <returns>If the <see cref="World"/> was found with the given ID</returns>
		public bool TryGetWorld(int worldId, [MaybeNullWhen(false)] out World world)
		{
			lock (_worldLock)
			{
				return _worldMap.TryGetValue(worldId, out world);
			}
		}

		public void Update()
		{
			lock (_worldLock)
			{
				foreach (var world in _worldList)
				{
					world.Update();
				}
			}
		}
	}
}
