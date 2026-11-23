using Silo;
using Xunit;

public class ProgressRingTests
{
    sealed class FixedClock(TimeSpan now) : IAnimationClock
    {
        public TimeSpan Now { get; } = now;
    }

    [Fact]
    public void An_indeterminate_active_ring_is_animating_and_the_others_are_not()
    {
        Assert.True(new ProgressRing().IsAnimating);
        Assert.False(new ProgressRing { Value = 0.3 }.IsAnimating);
        Assert.False(new ProgressRing { IsActive = false }.IsAnimating);
    }

    [Fact]
    public void The_value_is_clamped_and_can_be_cleared()
    {
        var ring = new ProgressRing { Value = 4 };

        Assert.Equal(1, ring.Value);

        ring.Value = -1;

        Assert.Equal(0, ring.Value);

        ring.Value = null;

        Assert.Null(ring.Value);
    }

    [Fact]
    public void The_arc_turns_once_a_period_and_stays_between_30_and_270_degrees()
    {
        var (start, _) = ProgressRing.ArcAt(TimeSpan.FromMilliseconds(300), 1200);

        Assert.Equal(90, start, 6);

        for (var ms = 0; ms < 4000; ms += 37)
        {
            var (_, sweep) = ProgressRing.ArcAt(TimeSpan.FromMilliseconds(ms), 1200);

            Assert.InRange(sweep, 29.999, 270.001);
        }
    }

    [Fact]
    public void A_Control_reads_the_clock_of_its_ancestors_and_has_time_zero_without_one()
    {
        var root = new Box();
        var ring = new ProgressRing();

        root.Children.Add(ring);

        Assert.Equal(TimeSpan.Zero, ring.AnimationTime);

        root.AnimationClock = new FixedClock(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), ring.AnimationTime);
    }

    [Fact]
    public void Animations_find_an_animating_Control_anywhere_in_the_tree()
    {
        var root = new Box();
        var inner = new Box();

        root.Children.Add(inner);

        Assert.False(Animations.IsAnimating(root));

        inner.Children.Add(new ProgressRing());

        Assert.True(Animations.IsAnimating(root));
    }

    [Fact]
    public void An_overlay_that_animates_is_reported_by_the_layer()
    {
        var layer = new OverlayLayer();

        layer.Show(new ProgressRing(), 0, 0);

        Assert.True(layer.IsAnimating);
        Assert.False(new OverlayLayer().IsAnimating);
    }
}
