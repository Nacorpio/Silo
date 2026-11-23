namespace Silo;

/// <summary>An <see cref="IAnimationClock"/> that only moves when it is told to, so that tests and screenshots can show an animation at an exact time.</summary>
/// <example>
/// <code language="csharp">
/// var clock = new ManualAnimationClock();
///
/// root.AnimationClock = clock;
/// root.ApplyStyles(resolver);
///
/// button.AddClass("active");
/// root.ApplyStyles(resolver);
///
/// Animations.Advance(root, TimeSpan.FromMilliseconds(75));   // half way through a 150 ms transition
/// </code>
/// </example>
public sealed class ManualAnimationClock : IAnimationClock
{
    /// <summary>Gets or sets the time. It starts at zero.</summary>
    public TimeSpan Now { get; set; }

    /// <summary>Moves the time forward.</summary>
    /// <param name="by">How far to move it.</param>
    public void Advance(TimeSpan by) => Now += by;
}
