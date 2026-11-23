namespace Silo;

/// <summary>Helpers that a window uses to run the animations of the Controls in it.</summary>
/// <remarks>A window asks <see cref="IsAnimating"/> after each frame. While it is <see langword="true"/> the window draws a frame every <see cref="FrameMilliseconds"/>, otherwise it sleeps until something happens.</remarks>
public static class Animations
{
    /// <summary>The time between two frames of an animation, in milliseconds.</summary>
    public const int FrameMilliseconds = 16;

    /// <summary>Tells whether a Control or any Control below it is animating.</summary>
    /// <param name="root">The Control to look at.</param>
    /// <returns><see langword="true"/> when one of them has <see cref="Control.IsAnimating"/> set.</returns>
    public static bool IsAnimating(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.IsAnimating)
            return true;

        foreach (var child in root.Children)
            if (IsAnimating((Control)child))
                return true;

        return false;
    }
}
