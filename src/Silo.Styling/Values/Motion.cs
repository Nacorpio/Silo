namespace Silo.Styling.Values;

/// <summary>
/// How a change takes place over time: for a length of time along an <see cref="Easing"/> curve, or driven by a <see cref="Spring"/>
/// that decides its own length.
/// </summary>
public sealed record Motion
{
    /// <summary>Creates a motion that lasts a fixed time.</summary>
    /// <param name="duration">The time the change takes. Zero or less makes the change instant.</param>
    /// <param name="easing">The curve. <see cref="Easing.Ease"/> when <see langword="null"/>.</param>
    public Motion(TimeSpan duration, Easing? easing = null)
    {
        Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        Easing = easing ?? Easing.Ease;
    }

    /// <summary>Creates a motion that is driven by a spring.</summary>
    /// <param name="spring">The spring.</param>
    public Motion(Spring spring)
    {
        Spring = spring;
        Easing = Easing.Linear;
    }

    /// <summary>Gets the time a fixed motion takes. It is zero for a spring, which takes as long as it takes: see <see cref="DurationFor"/>.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets the curve of a fixed motion.</summary>
    public Easing Easing { get; }

    /// <summary>Gets the spring that drives the motion, or <see langword="null"/> for a fixed motion.</summary>
    public Spring? Spring { get; }

    /// <summary>Gets whether the change happens at once.</summary>
    public bool IsInstant => Spring is null && Duration == TimeSpan.Zero;

    /// <summary>Works out how long the motion takes.</summary>
    /// <param name="velocity">The speed at the start in changes per second. Only a spring uses it.</param>
    /// <returns>The duration, or the time that a spring needs to settle.</returns>
    public TimeSpan DurationFor(double velocity = 0) => Spring is { } spring ? spring.SettleTime(velocity) : Duration;

    /// <summary>Works out how much of the change is shown.</summary>
    /// <param name="elapsed">The time since the motion started, after any delay.</param>
    /// <param name="velocity">The speed at the start in changes per second. Only a spring uses it.</param>
    /// <returns>The fraction of the change: 0 at the start, 1 at the end, and outside 0..1 where the curve overshoots.</returns>
    public double Progress(TimeSpan elapsed, double velocity = 0)
    {
        if (elapsed <= TimeSpan.Zero)
            return 0;

        if (Spring is { } spring)
            return spring.Progress(elapsed.TotalSeconds, velocity);

        return elapsed >= Duration ? 1 : Easing.Evaluate(elapsed.TotalSeconds / Duration.TotalSeconds);
    }

    /// <summary>Works out how fast the change is shown.</summary>
    /// <param name="elapsed">The time since the motion started, after any delay.</param>
    /// <param name="velocity">The speed at the start in changes per second. Only a spring uses it.</param>
    /// <returns>The speed in changes per second: 1 means the whole change in one second.</returns>
    public double SpeedAt(TimeSpan elapsed, double velocity = 0)
    {
        if (Spring is { } spring)
            return spring.SpeedAt(Math.Max(0, elapsed.TotalSeconds), velocity);

        if (Duration == TimeSpan.Zero || elapsed < TimeSpan.Zero || elapsed >= Duration)
            return 0;

        var step = TimeSpan.FromMilliseconds(1);

        return (Progress(elapsed + step) - Progress(elapsed)) / step.TotalSeconds;
    }
}
