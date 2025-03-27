namespace Arch.Core;

/// <summary>
///     Marks a particular public method on a <see cref="Entities"/> as causing a structural change.
///     Structural changes must never be invoked as another thread is accessing the <see cref="Entities"/> in any way.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StructuralChangeAttribute : Attribute
{
}
