namespace Silo;

/// <summary>Describes a tab whose close button was clicked.</summary>
/// <param name="index">The index of the tab.</param>
public sealed class TabCloseRequestedEventArgs(int index) : EventArgs
{
    /// <summary>Gets the index of the tab.</summary>
    public int Index { get; } = index;
}
