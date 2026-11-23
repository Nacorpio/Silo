namespace Silo;

/// <summary>Tells animated Controls what time it is. The platform window supplies one, so that Controls draw the same frame for the same time.</summary>
/// <remarks>
/// A Control reads it through <see cref="Control.AnimationClock"/>. Without a clock, time stands still at 0, which is what tests and
/// screenshots see. The clock is only the time: a Control that wants new frames while it is shown says so with
/// <see cref="Control.IsAnimating"/>, and the window draws frames about every 16 milliseconds as long as any Control does.
/// </remarks>
/// <example>
/// <code language="csharp">
/// sealed class FixedClock(TimeSpan now) : IAnimationClock
/// {
///     public TimeSpan Now { get; } = now;
/// }
///
/// root.AnimationClock = new FixedClock(TimeSpan.FromMilliseconds(250));
/// </code>
/// </example>
public interface IAnimationClock
{
    /// <summary>Gets the time since an arbitrary start that does not change while the window runs.</summary>
    TimeSpan Now { get; }
}
