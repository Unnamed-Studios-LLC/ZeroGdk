namespace ZeroGdk.Server
{
	/// <summary>
	/// Represents a base class for a game or simulation system that operates within a <see cref="World"/> context.
	/// Systems can be started, stopped, and updated during the world's lifecycle.
	/// </summary>
	public abstract class System
	{
		/// <summary>
		/// The world this system is currently associated with. Set when the system is added to a world.
		/// </summary>
		public World? World { get; internal set; }

		/// <summary>
		/// Called once when the system is first started. Override to perform initialization logic.
		/// </summary>
		public virtual void Start() { }

		/// <summary>
		/// Called once when the system is stopped. Override to perform cleanup logic.
		/// </summary>
		public virtual void Stop() { }

		/// <summary>
		/// Called on every update tick. Override to implement system behavior during each frame or simulation step.
		/// </summary>
		public virtual void Update() { }
	}
}
