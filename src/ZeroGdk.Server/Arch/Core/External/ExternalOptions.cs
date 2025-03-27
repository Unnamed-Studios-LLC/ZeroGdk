namespace Arch.Core.External;

public sealed class ExternalOptions
{
    public bool AllowChanges { get; set; }

    public void ThrowIfBlocked()
    {
        if (!AllowChanges)
        {
            throw new InvalidOperationException("Entity structural change is not currently allowed!");
        }
    }
}
