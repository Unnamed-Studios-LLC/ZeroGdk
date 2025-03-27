namespace Arch.Core;

/// <summary>
/// Tags a class as containing extensions for <see cref="Entities"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class WorldExtensionsAttribute : Attribute
{
}
