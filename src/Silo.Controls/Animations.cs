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

        if (root.IsAnimating || root.HasActiveTransitions)
            return true;

        foreach (var child in root.Children)
            if (IsAnimating((Control)child))
                return true;

        return false;
    }

    /// <summary>
    /// Lets every Control in a tree act on the passing of time: running transitions move to the current time, and <see cref="Control.OnFrame"/> is
    /// called on each Control.
    /// </summary>
    /// <param name="root">The root of the tree.</param>
    /// <returns>
    /// <see langword="true"/> when a Control asked for styles to be applied again. Transitions only change what is drawn, so they do not
    /// cause this, but they do need a new frame.
    /// </returns>
    public static bool Tick(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        root.AdvanceTransitions();

        var changed = root.OnFrame();

        foreach (var child in root.Children.ToArray())
            changed |= Tick((Control)child);

        return changed;
    }

    /// <summary>Moves a tree forward in time on its <see cref="ManualAnimationClock"/> and lets it act on that, for tests and screenshots.</summary>
    /// <param name="root">The root of the tree, whose <see cref="Element.AnimationClock"/> has to be a <see cref="ManualAnimationClock"/>.</param>
    /// <param name="by">How far to move time.</param>
    /// <returns>The same as <see cref="Tick"/>.</returns>
    /// <exception cref="InvalidOperationException">The clock of the tree is not a <see cref="ManualAnimationClock"/>.</exception>
    public static bool Advance(Control root, TimeSpan by)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.AnimationClock is not ManualAnimationClock clock)
        {
            throw new InvalidOperationException("Animations.Advance needs the tree to have a ManualAnimationClock.");
        }

        clock.Advance(by);

        return Tick(root);
    }
}
