using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZeroGdk.Client;
using ZeroGdk.Client.Data;
using EntityData = ZeroGdk.Client.Data.EntityData;

namespace ZeroGdk.Server
{
	/// <summary>
	/// Represents a simulation world on the server, managing clients, systems, and entity data.
	/// Provides mechanisms for handling persistent data, event propagation, and system management.
	/// </summary>
	public sealed class World
	{
		private readonly ILogger<World> _logger;
		private readonly DataEncoding _dataEncoding;
		private readonly List<Client> _clients = [];
		private readonly List<WorldSystem> _systems = [];
		//private int _systemUpdateIndex = 0; // part of remove logic
		private readonly Dictionary<int, EntityData> _entityDataMap = [];

		internal World(int worldId,
			string factoryRoute,
			IServiceProvider serviceProvider,
			ILogger<World> logger,
			IOptions<EntitiesOptions> entitiesOptions,
			DataEncoding dataEncoding)
		{
			_logger = logger;
			_dataEncoding = dataEncoding;
			Services = serviceProvider;
			WorldId = worldId;
			Entities = Entities.Create(
				chunkSizeInBytes: entitiesOptions.Value.ChunkSizeInBytes,
				minimumAmountOfEntitiesPerChunk: entitiesOptions.Value.MinimumAmountOfEntitiesPerChunk,
				archetypeCapacity: entitiesOptions.Value.ArchetypeCapacity,
				entityCapacity: entitiesOptions.Value.EntityCapacity
			);
			FactoryRoute = factoryRoute;
			Entities.SubscribeEntityDestroyed(OnEntityDestroyed);
		}

		/// <summary>
		/// Provides access to application-level services for dependency resolution and system creation.
		/// </summary>
		public IServiceProvider Services { get; }

		/// <summary>
		/// The globally unique identifier assigned to this world instance.
		/// </summary>
		public int WorldId { get; }

		/// <summary>
		/// The Arch-based entity-component storage and processing system associated with this world.
		/// </summary>
		public Entities Entities { get; private set; }

		/// <summary>
		/// The maximum maount of clients that can be added to this world, <c>-1</c> for no limit.
		/// </summary>
		public int MaxClients { get; private set; } = -1;

		/// <summary>
		/// A list of all clients in the world
		/// </summary>
		public IReadOnlyList<Client> Clients => _clients;

		/// <summary>
		/// The route identifier of the factory responsible for creating this world instance.
		/// </summary>
		internal string? FactoryRoute { get; }

		/// <summary>
		/// <c>true</c> if this <see cref="World"/> has started
		/// </summary>
		internal bool Started { get; set; } = false;

		/// <summary>
		/// Registers and adds a new <see cref="WorldSystem"/> instance of type <typeparamref name="T"/> created using the application services.
		/// Can only be called during <see cref="WorldFactory"/>.CreateAsync.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="WorldSystem"/> to add.</typeparam>
		/// <exception cref="ArgumentNullException">Thrown if the resolved system is null.</exception>
		/// <exception cref="WorldStartedException">Thrown if the world has already started</exception>
		public void AddSystem<T>() where T : WorldSystem
		{
			var system = Services.GetRequiredService<T>();
			AddSystem(system);
		}

		/// <summary>
		/// Registers and adds the specified <see cref="WorldSystem"/> instance to this world.
		/// Can only be called during <see cref="WorldFactory"/>.CreateAsync.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="WorldSystem"/> to add.</typeparam>
		/// <param name="system">The system instance to add.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="system"/> is null.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the system is already associated with a world.</exception>
		/// <exception cref="WorldStartedException">Thrown if the world has already started.</exception>
		public void AddSystem<T>(T system) where T : WorldSystem
		{
			ArgumentNullException.ThrowIfNull(system);

			if (system.World != null)
			{
				throw new InvalidOperationException("System already added to a world");
			}

			if (Started)
			{
				throw new WorldStartedException(this);
			}

			system.World = this;
			_systems.Add(system);
		}

		/// <summary>
		/// Retrieves persistent network data for the specified entity.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to retrieve.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <returns>The persistent data of type <typeparamref name="T"/> associated with the entity.</returns>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="DataNotFound">Thrown if the persistent data is not found.</exception>
		public T GetPersistent<T>(Entity entity) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				if (!entityData.TryReadPersistent(dataType, _dataEncoding.Sizes, out var data))
				{
					throw new DataNotFound(typeof(T));
				}
				return data;
			}
		}

		/// <summary>
		/// Retrieves persistent network data as a span for the specified entity.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to retrieve.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="dataSpan">A span where the persistent data will be written.</param>
		/// <returns>The length of the data written to the span.</returns>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="DataNotFound">Thrown if the persistent data is not found.</exception>
		public ushort GetPersistent<T>(Entity entity, Span<T> dataSpan) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				if (!entityData.TryReadPersistent(dataType, _dataEncoding.Sizes, dataSpan, out var length))
				{
					throw new DataNotFound(typeof(T));
				}
				return length;
			}
		}

		/// <summary>
		/// Retrieves the first instance of a system that is of type <typeparamref name="T"/> 
		/// from the internal collection of systems.
		/// </summary>
		/// <typeparam name="T">
		/// The type of system to retrieve. Must be derived from the base <see cref="WorldSystem"/> class.
		/// </typeparam>
		/// <returns>
		/// The first system of type <typeparamref name="T"/> found, or <c>null</c> if no matching system exists.
		/// </returns>
		public T? GetSystem<T>() where T : WorldSystem
		{
			return _systems.OfType<T>().FirstOrDefault();
		}

		/// <summary>
		/// Retrieves all instances of systems that are of type <typeparamref name="T"/> 
		/// from the internal collection of systems.
		/// </summary>
		/// <typeparam name="T">
		/// The type of systems to retrieve. Must be derived from the base <see cref="WorldSystem"/> class.
		/// </typeparam>
		/// <returns>
		/// An enumerable collection of systems of type <typeparamref name="T"/>. 
		/// If no matching systems are found, the returned collection will be empty.
		/// </returns>
		public IEnumerable<T> GetSystems<T>() where T : WorldSystem
		{
			return _systems.OfType<T>();
		}

		/// <summary>
		/// Sends a one-time network event containing a single instance of data for the specified entity.
		/// Event data is transmitted once and is not retained.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type being pushed.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">The data to send.</param>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="MaxDataException">Thrown if the data exceeds allowed size limits.</exception>
		public void PushEvent<T>(Entity entity, in T data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				entityData.WriteEvent(Time.Tick, dataType, in data);
			}
		}

		/// <summary>
		/// Sends a one-time network event containing a span of data for the specified entity.
		/// Event data is transmitted once and is not retained.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type being pushed.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">The span of data to send.</param>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="DataSpanLengthException">Thrown if the span exceeds the allowed maximum length.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="MaxDataException">Thrown if the data exceeds allowed size limits.</exception>
		public void PushEvent<T>(Entity entity, in ReadOnlySpan<T> data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (data.Length > dataType.MaxSpanLength)
			{
				throw new DataSpanLengthException(typeof(T), dataType.MaxSpanLength);
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				entityData.WriteEvent(Time.Tick, dataType, in data);
			}
		}

		/// <summary>
		/// Sends persistent network data for the specified entity.
		/// Persistent data is sent to clients when they first observe the entity or when the data changes.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type being pushed.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">The data to persist.</param>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="MaxDataException">Thrown if the data exceeds allowed size limits.</exception>
		public void PushPersistent<T>(Entity entity, in T data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				entityData.WritePersistentChange(Time.Tick, dataType, in data, _dataEncoding.Sizes);
			}
		}

		/// <summary>
		/// Sends persistent network data as a span for the specified entity.
		/// This data is retained and sent when the entity is first observed or when the span changes.
		/// Spans are treated as a distinct data type from individual instances.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type being pushed.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">The span of data to persist.</param>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="DataSpanLengthException">Thrown if the span exceeds the allowed maximum length.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="MaxDataException">Thrown if the data exceeds allowed size limits.</exception>
		public void PushPersistent<T>(Entity entity, in ReadOnlySpan<T> data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (data.Length > dataType.MaxSpanLength)
			{
				throw new DataSpanLengthException(typeof(T), dataType.MaxSpanLength);
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				entityData.WritePersistentChange(Time.Tick, dataType, in data, _dataEncoding.Sizes);
			}
		}

		/*
		 * Disabled system removal for now, since you can't unregister from events
		 * 
		/// <summary>
		/// Removes a <see cref="System"/> of a given type
		/// </summary>
		/// <typeparam name="T">Type of <see cref="System"/> to remove</typeparam>
		public void RemoveSystem<T>() where T : System
		{
			for (int i = 0; i < _systems.Count; i++)
			{
				if (_systems[i] is T system)
				{
					_systems.RemoveAt(i);
					system.World = null;
					if (i < _systemUpdateIndex)
					{
						_systemUpdateIndex--;
					}
					return;
				}
			}
		}

		/// <summary>
		/// Removes a <see cref="System"/>
		/// </summary>
		/// <typeparam name="T">Type of <see cref="System"/> to remove</typeparam>
		/// <exception cref="ArgumentNullException"/>
		/// <exception cref="InvalidOperationException"/>
		public void RemoveSystem<T>(T system) where T : System
		{
			ArgumentNullException.ThrowIfNull(system);

			if (system.World != this)
			{
				throw new InvalidOperationException("System not added to this world!");
			}

			for (int i = 0; i < _systems.Count; i++)
			{
				if (_systems[i] == system)
				{
					_systems.RemoveAt(i);
					system.World = null;
					if (i < _systemUpdateIndex)
					{
						_systemUpdateIndex--;
					}
					return;
				}
			}
		}

		/// <summary>
		/// Removes all matching <see cref="System"/> of a given type
		/// </summary>
		/// <typeparam name="T">Type of <see cref="System"/> to remove</typeparam>
		public void RemoveSystems<T>() where T : System
		{
			for (int i = 0; i < _systems.Count; i++)
			{
				if (_systems[i] is T system)
				{
					_systems.RemoveAt(i);
					system.World = null;
					i--;
					if (i < _systemUpdateIndex)
					{
						_systemUpdateIndex--;
					}
				}
			}
		}
		*/

		/// <summary>
		/// Sets persistent data for the specified entity without triggering any update to network clients.
		/// Useful when you want to manually control when data updates are sent.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to store.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">The data to store persistently.</param>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="MaxDataException">Thrown if the data exceeds allowed size limits.</exception>
		public void SetPersistent<T>(Entity entity, in T data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				entityData.WritePersistentOnly(dataType, in data, _dataEncoding.Sizes);
			}
		}

		/// <summary>
		/// Sets a span of persistent data for the specified entity without pushing updates to clients.
		/// This allows control over update timing. Spans are treated separately from single-instance data.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to store.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">The span of data to store persistently.</param>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="DataSpanLengthException">Thrown if the span exceeds the allowed maximum length.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		/// <exception cref="MaxDataException">Thrown if the data exceeds allowed size limits.</exception>
		public void SetPersistent<T>(Entity entity, in ReadOnlySpan<T> data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (data.Length > dataType.MaxSpanLength)
			{
				throw new DataSpanLengthException(typeof(T), dataType.MaxSpanLength);
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				entityData.WritePersistentOnly(dataType, in data, _dataEncoding.Sizes);
			}
		}

		/// <summary>
		/// Attempts to retrieve persistent data for the specified entity.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to retrieve.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="data">When this method returns, contains the persistent data if found.</param>
		/// <returns><c>true</c> if the persistent data was successfully retrieved; otherwise, <c>false</c>.</returns>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		public bool TryGetPersistent<T>(Entity entity, out T data) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				if (!entityData.TryReadPersistent(dataType, _dataEncoding.Sizes, out data))
				{
					return false;
				}
				return true;
			}
		}

		/// <summary>
		/// Attempts to retrieve persistent data as a span for the specified entity.
		/// </summary>
		/// <typeparam name="T">The unmanaged data type to retrieve.</typeparam>
		/// <param name="entity">The target entity.</param>
		/// <param name="dataSpan">A span in which the persistent data will be written.</param>
		/// <param name="length">When this method returns, contains the length of the data written to the span.</param>
		/// <returns><c>true</c> if the persistent data was successfully retrieved; otherwise, <c>false</c>.</returns>
		/// <exception cref="DataNotRegisteredException">Thrown if the data type is not registered.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if the entity is not valid or alive.</exception>
		public bool TryGetPersistent<T>(Entity entity, Span<T> dataSpan, out ushort length) where T : unmanaged
		{
			if (!_dataEncoding.TryGetType<T>(out var dataType))
			{
				throw new DataNotRegisteredException(typeof(T));
			}

			if (!Entities.IsAlive(entity))
			{
				throw new EntityNotFoundException(entity);
			}

			var entityData = GetOrCreateData(entity.Id);
			lock (entityData)
			{
				if (!entityData.TryReadPersistent(dataType, _dataEncoding.Sizes, dataSpan, out length))
				{
					return false;
				}
				return true;
			}
		}

		/// <summary>
		/// Releases all resources used by the world, including disposing the entity system.
		/// </summary>
		internal void Dispose()
		{
			Entities.Dispose();
		}

		/// <summary>
		/// Starts the world by marking it as started and invoking the start method on all registered systems.
		/// </summary>
		internal void Start()
		{
			Started = true;
			foreach (var system in _systems)
			{
				try
				{
					system.Start();
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An error occurred while executing '{method}' on system of type '{systemType}'", nameof(WorldSystem.Start), system.GetType().FullName);
				}
			}
		}

		/// <summary>
		/// Stops the world by invoking the stop method on all registered systems.
		/// </summary>
		internal void Stop()
		{
			foreach (var system in _systems)
			{
				try
				{
					system.Stop();
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An error occurred while executing '{method}' on system of type '{systemType}'", nameof(WorldSystem.Stop), system.GetType().FullName);
				}
			}
		}

		internal bool TryGetEntityData(int entityId, [MaybeNullWhen(false)] out EntityData entityData)
		{
			return _entityDataMap.TryGetValue(entityId, out entityData);
		}

		/// <summary>
		/// Updates the world by invoking the update method on all registered systems.
		/// </summary>
		internal void Update()
		{
			foreach (var system in _systems)
			{
				try
				{
					system.Update();
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An error occurred while executing '{method}' on system of type '{systemType}'", nameof(WorldSystem.Update), system.GetType().FullName);
				}
			}
		}

		/// <summary>
		/// Retrieves the <see cref="EntityData"/> associated with the specified entity ID.
		/// If no data exists, a new instance is created and stored.
		/// </summary>
		/// <param name="entityId">The identifier of the entity.</param>
		/// <returns>The <see cref="EntityData"/> associated with the entity.</returns>
		private EntityData GetOrCreateData(int entityId)
		{
			if (!_entityDataMap.TryGetValue(entityId, out var entityData))
			{
				entityData = new(entityId);
				_entityDataMap.Add(entityId, entityData);
			}
			return entityData;
		}

		/// <summary>
		/// Handles the cleanup of entity data when an entity is destroyed.
		/// Removes the entity's persistent data from the internal storage.
		/// </summary>
		/// <param name="entity">The entity that was destroyed.</param>
		private void OnEntityDestroyed(in Entity entity)
		{
			if (!_entityDataMap.Remove(entity.Id, out var entityData))
			{
				return;
			}

			entityData.Clear();
		}
	}
}
