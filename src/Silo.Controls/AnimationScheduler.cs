namespace Silo;

/// <summary>
/// Runs the animations of one window: the transitions of its Controls, the animated Controls, and the Controls in its overlays. The platform
/// window asks it before each frame whether anything animates, and lets it move everything to the current time.
/// </summary>
/// <remarks>
/// It does not draw by itself. A window that has nothing animating sleeps until something happens, so an idle app costs no time. Time comes
/// from the <see cref="IAnimationClock"/> of the root, which is the real time for a window, so a frame that is late never slows an animation
/// down: the next frame just shows the later time.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var scheduler = new AnimationScheduler(root, overlays);
///
/// if (scheduler.Tick())
///     root.ApplyStyles(resolver);
///
/// var moreFrames = scheduler.IsAnimating;
/// </code>
/// </example>
public sealed class AnimationScheduler(Control root, OverlayLayer? overlays = null)
{
    /// <summary>Gets the time between two frames of an animation, in milliseconds, when the display refresh rate is not known.</summary>
    public const int DefaultFrameMilliseconds = Animations.FrameMilliseconds;

    /// <summary>Gets or sets the time between two frames while something animates, in milliseconds. It is 16 (60 frames per second) until the platform window sets the refresh rate of the display.</summary>
    public int FrameMilliseconds { get; set; } = DefaultFrameMilliseconds;

    /// <summary>Gets or sets whether the user prefers little motion: transitions other than those of <c>opacity</c> are skipped. The default is <see langword="false"/>.</summary>
    public bool ReducedMotion
    {
        get => root.ReducedMotion;
        set => root.ReducedMotion = value;
    }

    /// <summary>Gets whether anything in the tree or the overlays animates, so that another frame is due.</summary>
    public bool IsAnimating => Animations.IsAnimating(root) || (overlays?.IsAnimating ?? false);

    /// <summary>Moves everything to the current time.</summary>
    /// <returns><see langword="true"/> when a Control asked for styles to be applied again.</returns>
    public bool Tick() => Animations.Tick(root) | (overlays?.Tick() ?? false);
}
