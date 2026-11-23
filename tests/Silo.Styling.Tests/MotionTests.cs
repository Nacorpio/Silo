using Silo.Styling.Values;
using Xunit;

public class MotionTests
{
    [Theory]
    [InlineData("linear")]
    [InlineData("ease")]
    [InlineData("ease-in")]
    [InlineData("ease-out")]
    [InlineData("ease-in-out")]
    [InlineData("cubic-bezier(0.2, 0, 0, 1)")]
    [InlineData("steps(4)")]
    public void Every_curve_starts_at_zero_and_ends_at_one(string text)
    {
        Assert.True(Easing.TryParse(text, out var easing));
        Assert.Equal(0, easing.Evaluate(0), 6);
        Assert.Equal(1, easing.Evaluate(1), 6);
    }

    [Fact]
    public void Linear_is_even_and_the_ease_curves_have_the_expected_shape()
    {
        Assert.Equal(0.25, Easing.Linear.Evaluate(0.25), 6);
        Assert.True(Easing.EaseIn.Evaluate(0.5) < 0.5);
        Assert.True(Easing.EaseOut.Evaluate(0.5) > 0.5);
        Assert.Equal(0.5, Easing.EaseInOut.Evaluate(0.5), 3);
        Assert.True(Easing.Ease.Evaluate(0.5) > 0.5);
    }

    [Fact]
    public void A_cubic_bezier_can_overshoot_and_keeps_x_in_range()
    {
        var overshoot = Easing.CubicBezier(0.3, 1.8, 0.6, 1);

        Assert.Contains(Enumerable.Range(1, 99), i => overshoot.Evaluate(i / 100.0) > 1.01);
        Assert.True(Easing.TryParse("cubic-bezier(2, 0, -1, 1)", out var clamped));
        Assert.Equal(1, clamped.Evaluate(1), 6);
    }

    [Fact]
    public void Steps_jump_at_the_end_or_the_start_of_each_step()
    {
        var end = Easing.Steps(4);
        var start = Easing.Steps(4, jumpStart: true);

        Assert.Equal(0, end.Evaluate(0.24), 6);
        Assert.Equal(0.25, end.Evaluate(0.25), 6);
        Assert.Equal(0.75, end.Evaluate(0.99), 6);
        Assert.Equal(0.25, start.Evaluate(0.01), 6);
        Assert.Equal(0.5, start.Evaluate(0.25), 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bounce")]
    [InlineData("cubic-bezier(0, 0, 1)")]
    [InlineData("cubic-bezier(a, 0, 1, 1)")]
    [InlineData("steps(0)")]
    [InlineData("steps(2, middle)")]
    public void Bad_curves_are_rejected(string text)
    {
        Assert.False(Easing.TryParse(text, out _));
    }

    [Fact]
    public void A_spring_without_damping_loss_overshoots_and_settles_on_the_target()
    {
        var spring = new Spring(170, 12);

        Assert.Equal(0, spring.Progress(0), 6);
        Assert.Contains(Enumerable.Range(1, 100), i => spring.Progress(i / 100.0) > 1.01);

        var settle = spring.SettleTime();

        Assert.InRange(settle.TotalSeconds, 0.3, 5);
        Assert.Equal(1, spring.Progress(settle.TotalSeconds + 1), 3);
    }

    [Theory]
    [InlineData(170, 26, 1)]
    [InlineData(170, 2 * 13.038404810405298, 1)]
    [InlineData(300, 10, 2)]
    public void A_spring_of_any_damping_never_leaves_the_range_it_should_settle_in(double stiffness, double damping, double mass)
    {
        var spring = new Spring(stiffness, damping, mass);

        Assert.True(spring.IsValid);
        Assert.Equal(1, spring.Progress(spring.SettleTime().TotalSeconds + 2), 3);
    }

    [Fact]
    public void A_critically_damped_spring_does_not_overshoot()
    {
        var spring = new Spring(100, 20);

        Assert.DoesNotContain(Enumerable.Range(0, 300), i => spring.Progress(i / 100.0) > 1.0001);
    }

    [Fact]
    public void Starting_speed_moves_the_start_of_the_curve()
    {
        var spring = new Spring(170, 18);

        Assert.True(spring.Progress(0.02, velocity: 5) > spring.Progress(0.02));
        Assert.True(spring.Progress(0.02, velocity: -5) < spring.Progress(0.02));
        Assert.Equal(5, spring.SpeedAt(0, velocity: 5), 6);
    }

    [Theory]
    [InlineData("spring(170 18)", 170, 18, 1)]
    [InlineData("spring(170, 18, 2)", 170, 18, 2)]
    public void Springs_are_read(string text, double stiffness, double damping, double mass)
    {
        Assert.True(Spring.TryParse(text, out var spring));
        Assert.Equal(new Spring(stiffness, damping, mass), spring);
    }

    [Theory]
    [InlineData("spring(170)")]
    [InlineData("spring(0 18)")]
    [InlineData("spring(170 -1)")]
    [InlineData("spring(170 18 0)")]
    [InlineData("spring(a b)")]
    [InlineData("bounce(1 2)")]
    public void Bad_springs_are_rejected(string text)
    {
        Assert.False(Spring.TryParse(text, out _));
    }

    [Fact]
    public void A_fixed_motion_follows_its_curve_and_ends_exactly()
    {
        var motion = new Motion(TimeSpan.FromMilliseconds(200), Easing.Linear);

        Assert.Equal(0, motion.Progress(TimeSpan.Zero));
        Assert.Equal(0.5, motion.Progress(TimeSpan.FromMilliseconds(100)), 6);
        Assert.Equal(1, motion.Progress(TimeSpan.FromMilliseconds(300)));
        Assert.Equal(TimeSpan.FromMilliseconds(200), motion.DurationFor());
        Assert.Equal(5, motion.SpeedAt(TimeSpan.FromMilliseconds(100)), 3);
        Assert.False(motion.IsInstant);
        Assert.True(new Motion(TimeSpan.Zero).IsInstant);
    }

    [Fact]
    public void A_spring_motion_takes_as_long_as_the_spring_needs()
    {
        var motion = new Motion(new Spring(170, 18));

        Assert.False(motion.IsInstant);
        Assert.Equal(new Spring(170, 18).SettleTime(), motion.DurationFor());
        Assert.Equal(new Spring(170, 18).Progress(0.1), motion.Progress(TimeSpan.FromMilliseconds(100)), 9);
    }

    [Fact]
    public void A_transition_list_is_read_with_durations_curves_delays_and_springs()
    {
        Assert.True(TransitionList.TryParse("background 150ms ease-out, opacity 0.1s linear 50ms, transform spring(170 18), all 1s", out var list));
        Assert.Equal(4, list.Entries.Count);

        var background = list.Find("background")!;

        Assert.Equal(TimeSpan.FromMilliseconds(150), background.Motion.Duration);
        Assert.Same(Easing.EaseOut, background.Motion.Easing);

        var opacity = list.Find("opacity")!;

        Assert.Equal(TimeSpan.FromMilliseconds(100), opacity.Motion.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(50), opacity.Delay);

        Assert.Equal(new Spring(170, 18), list.Find("transform")!.Motion.Spring);
        Assert.Equal(TimeSpan.FromSeconds(1), list.Find("width")!.Motion.Duration);
    }

    [Fact]
    public void The_curve_can_come_before_the_duration_and_a_later_entry_wins()
    {
        Assert.True(TransitionList.TryParse("all 1s, opacity ease-in 200ms, opacity 300ms", out var list));
        Assert.Equal(TimeSpan.FromMilliseconds(300), list.Find("opacity")!.Motion.Duration);
        Assert.Equal(TimeSpan.FromSeconds(1), list.Find("color")!.Motion.Duration);
        Assert.Null(TransitionList.None.Find("color"));
    }

    [Fact]
    public void None_is_an_empty_list_and_the_list_reads_back_from_its_text()
    {
        Assert.True(TransitionList.TryParse("none", out var none));
        Assert.Empty(none.Entries);
        Assert.Equal("none", none.ToString());

        Assert.True(TransitionList.TryParse("background 150ms ease-out 20ms, transform spring(170 18 1)", out var list));
        Assert.True(TransitionList.TryParse(list.ToString(), out var again));
        Assert.Equal(list, again);
    }

    [Theory]
    [InlineData("")]
    [InlineData("150ms")]
    [InlineData("background")]
    [InlineData("background 150ms 20ms 30ms")]
    [InlineData("background 150ms ease-out linear")]
    [InlineData("background spring(170 18) ease-out")]
    [InlineData("background fast")]
    [InlineData("background -150ms")]
    [InlineData("background 150ms, ")]
    public void Bad_transitions_are_rejected(string text)
    {
        Assert.False(TransitionList.TryParse(text, out var list));
        Assert.Empty(list.Entries);
    }

    [Fact]
    public void Numbers_colors_lengths_and_transforms_are_blended()
    {
        Assert.True(Interpolator.TryLerp(10.0, 20.0, 0.5, out var number));
        Assert.Equal(15.0, number);

        Assert.True(Interpolator.TryLerp(new Color(0, 0, 0), new Color(255, 100, 50, 0), 0.5, out var color));
        Assert.Equal(new Color(128, 50, 25, 128), color);

        Assert.True(Interpolator.TryLerp(new Length(10, LengthUnit.Px), new Length(20, LengthUnit.Px), 0.25, out var length));
        Assert.Equal(new Length(12.5, LengthUnit.Px), length);

        Assert.True(Interpolator.TryLerp(Transform.None, new Transform(10, 20, 90, 2, 3), 0.5, out var transform));
        Assert.Equal(new Transform(5, 10, 45, 1.5, 2), transform);

        Assert.True(Interpolator.TryLerp(
            new Thickness(new Length(0, LengthUnit.Px), new Length(4, LengthUnit.Px), new Length(0, LengthUnit.Px), new Length(8, LengthUnit.Px)),
            new Thickness(new Length(10, LengthUnit.Px), new Length(4, LengthUnit.Px), new Length(10, LengthUnit.Px), new Length(0, LengthUnit.Px)),
            0.5,
            out var thickness));
        Assert.Equal(new Length(5, LengthUnit.Px), ((Thickness)thickness!).Top);
        Assert.Equal(new Length(4, LengthUnit.Px), ((Thickness)thickness).Left);
    }

    [Fact]
    public void Overshoot_extrapolates_numbers_and_keeps_colors_in_range()
    {
        Assert.True(Interpolator.TryLerp(0.0, 10.0, 1.5, out var number));
        Assert.Equal(15.0, number);

        Assert.True(Interpolator.TryLerp(new Color(0, 0, 0), new Color(200, 200, 200), 1.5, out var color));
        Assert.Equal(new Color(255, 255, 255), color);
    }

    [Fact]
    public void Values_that_cannot_be_blended_are_refused()
    {
        Assert.False(Interpolator.CanInterpolate(new Length(10, LengthUnit.Px), new Length(50, LengthUnit.Percent)));
        Assert.False(Interpolator.CanInterpolate(null, new Length(50, LengthUnit.Px)));
        Assert.False(Interpolator.CanInterpolate(true, false));
        Assert.False(Interpolator.CanInterpolate("a", "b"));
        Assert.False(Interpolator.CanInterpolate(1.0, new Color(0, 0, 0)));
        Assert.True(Interpolator.CanInterpolate(1.0, 2.0));
    }
}
