using Silo.Styling.Values;

namespace Silo;

public partial class Element
{
    /// <summary>The transitions that are running, by property.</summary>
    Dictionary<StyledProperty, ActiveTransition>? transitions;

    /// <summary>The values that running transitions show now, by property. They take the place of the real value while the transition runs.</summary>
    Dictionary<StyledProperty, object?>? animatedValues;

    /// <summary>The clock that this Element reads, or <see langword="null"/> to use its parent's.</summary>
    IAnimationClock? animationClock;

    /// <summary>Whether reduced motion was set on this Element, or <see langword="null"/> to use its parent's.</summary>
    bool? reducedMotion;

    /// <summary>Whether Stylesheets were applied to this Element at least once. The first style never transitions from the defaults.</summary>
    bool hasBeenStyled;

    /// <summary>
    /// Gets or sets the clock that transitions and animated Controls read. When it is not set, the nearest ancestor's clock is used, so the
    /// platform window sets it once on the root. Without one, <see cref="AnimationTime"/> is 0 and properties change at once.
    /// </summary>
    public IAnimationClock? AnimationClock
    {
        get => animationClock ?? Parent?.AnimationClock;
        set => animationClock = value;
    }

    /// <summary>Gets the time on the <see cref="AnimationClock"/>, or 0 when there is none.</summary>
    public TimeSpan AnimationTime => AnimationClock?.Now ?? TimeSpan.Zero;

    /// <summary>
    /// Gets or sets whether the user prefers little motion. When it is set, transitions are skipped except those of <c>opacity</c>, so that
    /// things still fade but nothing slides, grows or spins. When it is not set, the nearest ancestor's value is used, so the platform window
    /// sets it once on the root. The default is <see langword="false"/>.
    /// </summary>
    public bool ReducedMotion
    {
        get => reducedMotion ?? Parent?.ReducedMotion ?? false;
        set => reducedMotion = value;
    }

    /// <summary>Gets whether a transition of this Element is running, so that the window has to keep drawing frames.</summary>
    public bool HasActiveTransitions => transitions is { Count: > 0 };

    /// <summary>
    /// Moves the transitions of this Element to the current time on the <see cref="AnimationClock"/>. The platform window calls it for
    /// every Element before each frame it draws while something animates.
    /// </summary>
    /// <returns><see langword="true"/> when a value that is shown changed.</returns>
    public bool AdvanceTransitions()
    {
        if (transitions is not { Count: > 0 })
        {
            return false;
        }

        var now = AnimationTime;
        var changed = false;

        foreach (var (property, transition) in transitions.ToArray())
        {
            var elapsed = now - transition.Start;

            if (elapsed < TimeSpan.Zero)
            {
                continue;
            }

            if (elapsed >= transition.Duration)
            {
                transitions.Remove(property);
                animatedValues!.Remove(property);
                OnPropertyChanged(property);

                changed = true;

                continue;
            }

            var progress = transition.Motion.Progress(elapsed, transition.Velocity);

            if (Interpolator.TryLerp(transition.From, transition.To, progress, out var value) && !Equals(animatedValues![property], value))
            {
                animatedValues[property] = value;
                OnPropertyChanged(property);

                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Starts, retargets or cancels the transition of a property whose value was changed.</summary>
    /// <param name="property">The property.</param>
    /// <param name="before">The value it was heading for until now.</param>
    /// <param name="after">The value it is heading for now.</param>
    void BeginTransition(StyledProperty property, object? before, object? after)
    {
        var spec = property == TransitionProperty || !hasBeenStyled || AnimationClock is null
            ? null
            : ((TransitionList)GetTargetObject(TransitionProperty)!).Find(property.Name);

        if (spec is null || spec.Motion.IsInstant && spec.Delay == TimeSpan.Zero || (ReducedMotion && property.Name != "opacity"))
        {
            CancelTransition(property);

            return;
        }

        // The transition starts from what is shown, which is the middle of the last transition when one was running.
        var from = animatedValues is not null && animatedValues.TryGetValue(property, out var shown) ? shown : before;

        if (!Interpolator.CanInterpolate(from, after))
        {
            CancelTransition(property);

            return;
        }

        var now = AnimationTime;
        var velocity = 0.0;

        if (spec.Motion.Spring is not null && transitions is not null && transitions.TryGetValue(property, out var running)
            && from is double current && after is double target && Math.Abs(target - current) > 1e-9)
        {
            // A spring that is interrupted keeps its speed: the speed of the value is turned into changes per second of the new distance.
            velocity = running.ValueSpeed(now) / (target - current);
        }

        transitions ??= [];
        animatedValues ??= [];
        transitions[property] = new ActiveTransition(from, after, spec.Motion, now + spec.Delay, velocity);
        animatedValues[property] = from;
    }

    /// <summary>Stops the transition of a property and shows its value at once.</summary>
    /// <param name="property">The property.</param>
    void CancelTransition(StyledProperty property)
    {
        if (transitions is not null && transitions.Remove(property))
        {
            animatedValues!.Remove(property);
        }
    }

    /// <summary>A transition that is running.</summary>
    /// <param name="From">The value that is shown when it starts.</param>
    /// <param name="To">The value it ends on.</param>
    /// <param name="Motion">The timing.</param>
    /// <param name="Start">The time it starts, after the delay.</param>
    /// <param name="Velocity">For a spring, the speed at the start in changes per second.</param>
    sealed record ActiveTransition(object? From, object? To, Motion Motion, TimeSpan Start, double Velocity)
    {
        /// <summary>Gets how long it runs.</summary>
        public TimeSpan Duration { get; } = Motion.DurationFor(Velocity);

        /// <summary>Gets how fast a number changes, in units per second.</summary>
        /// <param name="now">The time.</param>
        /// <returns>The speed, or 0 when the values are not numbers or the transition has not started.</returns>
        public double ValueSpeed(TimeSpan now) =>
            From is double from && To is double to ? (to - from) * Motion.SpeedAt(now - Start, Velocity) : 0;
    }
}
