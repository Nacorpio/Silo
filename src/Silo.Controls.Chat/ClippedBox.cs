using Silo.Layout;

namespace Silo;

/// <summary>
/// A box that clips its children to its own size. A parent that wants to hide it places it with an empty rectangle, which then
/// draws nothing and takes no pointer input.
/// </summary>
internal sealed class ClippedBox : Box
{
    /// <inheritdoc/>
    protected override Rect? ChildClip => new Rect(0, 0, Bounds.Width, Bounds.Height);
}
