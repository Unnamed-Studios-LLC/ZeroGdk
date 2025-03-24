
using System.Runtime.CompilerServices;

namespace Benchmark;

/// <summary>
///     The <see cref="Entity"/> struct
///     represents a general-purpose object and can be assigned a set of components that act as data.
/// </summary>
[SkipLocalsInit]
public readonly struct Entity : IEquatable<Entity>, IComparable<Entity>
{
	/// <summary>
	///     Its Id, unique in its <see cref="Entities"/>.
	/// </summary>
	public readonly int Id = -1;

	/// <summary>
	///     The version of an entity.
	/// </summary>
	public readonly int Version;

	/// <summary>
	///     A null entity, used for comparison.
	/// </summary>
	public static readonly Entity Null = new(-1, 0, -1);

	/// <summary>
	///     Initializes a new instance of the <see cref="Entity"/> struct.
	/// </summary>
	/// <param name="id">Its unique id.</param>
	/// <param name="worldId">Its world id, not used for this entity since its pure ecs.</param>
	/// <param name="version">Its version.</param>
	internal Entity(int id, int worldId)
	{
		Id = id;
		Version = 1;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="Entity"/> struct.
	/// </summary>
	/// <param name="id">Its unique id.</param>
	/// <param name="worldId">Its world id, not used for this entity since its pure ecs.</param>
	/// <param name="version">Its version.</param>
	internal Entity(int id, int worldId, int version)
	{
		Id = id;
		Version = version;
	}

	/// <summary>
	///     Checks the <see cref="Entity"/> for equality with another one.
	/// </summary>
	/// <param name="other">The other <see cref="Entity"/>.</param>
	/// <returns>True if equal, false if not.</returns>
	public bool Equals(Entity other)
	{
		return ((Id ^ other.Id) | (Version ^ other.Version)) == 0;
	}

	/// <summary>
	///     Checks the <see cref="Entity"/> for equality with another object..
	/// </summary>
	/// <param name="obj">The other <see cref="Entity"/> object.</param>
	/// <returns>True if equal, false if not.</returns>
	public override bool Equals(object? obj)
	{
		return obj is Entity other && Equals(other);
	}

	/// <summary>
	///     Compares this <see cref="Entity"/> instace to another one for sorting and ordering.
	///     <remarks>Orders them by id. Ascending.</remarks>
	/// </summary>
	/// <param name="other">The other <see cref="Entity"/>.</param>
	/// <returns>A int indicating their order.</returns>
	public int CompareTo(Entity other)
	{
		return (Version.CompareTo(other.Version) << 8) | Id.CompareTo(other.Id);
	}

	/// <summary>
	///     Calculates the hash of this <see cref="Entity"/>.
	/// </summary>
	/// <returns>Its hash.</returns>

	public override int GetHashCode()
	{
		unchecked
		{
			// Overflow is fine, just wrap
			var hash = 17;
			hash = hash * 23 + Id;
			hash = hash * 23 + Version;
			return hash;
		}
	}

	/// <summary>
	///     Checks the left <see cref="Entity"/> for equality with the right one.
	/// </summary>
	/// <param name="left">The left <see cref="Entity"/>.</param>
	/// <param name="right">The right <see cref="Entity"/>.</param>
	/// <returns>True if both are equal, otherwise false.</returns>

	public static bool operator ==(Entity left, Entity right)
	{
		return left.Equals(right);
	}

	/// <summary>
	///     Checks the left <see cref="Entity"/> for unequality with the right one.
	/// </summary>
	/// <param name="left">The left <see cref="Entity"/>.</param>
	/// <param name="right">The right <see cref="Entity"/>.</param>
	/// <returns>True if both are unequal, otherwise false.</returns>

	public static bool operator !=(Entity left, Entity right)
	{
		return !left.Equals(right);
	}

	/// <summary>
	///     Converts this entity to a string.
	/// </summary>
	/// <returns>A string.</returns>
	public override string ToString()
	{
		return $"{nameof(Id)}: {Id}";
	}
}