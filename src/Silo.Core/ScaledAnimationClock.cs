namespace Silo;

/// <summary>
/// An <see cref="IAnimationClock"/> that runs faster or slower than another clock, or stands still, without ever jumping: changing the speed
/// only changes how fast time moves from then on. It is how a playground shows an animation in slow motion.
/// </summary>
/// <example>
/// <code language="csharp">
/// var clock = new ScaledAnimationClock(() =&gt; window.AnimationClock) { Speed = 0.25 };
///
/// stage.AnimationClock = clock;   // everything in the stage now runs at a quarter of the speed
/// </code>
/// </example>
/// <param name="source">Gives the clock to follow. It is asked every time, so it can return the clock of an ancestor that is not attached yet.</param>
public sealed class ScaledAnimationClock(Func<IAnimationClock?> source) : IAnimationClock
{
    /// <summary>The time on the source clock when this clock was last read.</summary>
    TimeSpan last;

    /// <summary>The time that this clock shows.</summary>
    TimeSpan shown;

    /// <summary>Whether <see cref="last"/> was set.</summary>
    bool started;

    /// <summary>Gets or sets how fast time moves compared to the source: 1 is the same speed, 0.5 half as fast. It is not negative.</summary>
    public double Speed { get; set; } = 1;

    /// <summary>Gets or sets whether time stands still.</summary>
    public bool IsPaused { get; set; }

    /// <inheritdoc/>
    public TimeSpan Now
    {
        get
        {
            var raw = source()?.Now ?? TimeSpan.Zero;

            if (!started)
            {
                (last, started) = (raw, true);

                return shown;
            }

            var passed = raw - last;

            last = raw;

            if (!IsPaused && passed > TimeSpan.Zero)
                shown += passed * Math.Max(0, Speed);

            return shown;
        }
    }
}
