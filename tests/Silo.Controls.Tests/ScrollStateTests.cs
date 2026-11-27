using Silo;
using Xunit;

public class ScrollStateTests
{
    static (ScrollState State, Action<double> SetExtent, Action<double> SetViewport) Make(double extent, double viewport)
    {
        var state = new ScrollState(() => extent, () => viewport);

        return (state, e => extent = e, v => viewport = v);
    }

    [Fact]
    public void Scrolling_stops_at_both_ends_and_reports_whether_it_moved()
    {
        var (state, _, _) = Make(500, 100);

        Assert.True(state.ScrollBy(30));
        Assert.Equal(30, state.Offset);
        Assert.True(state.ScrollBy(1000));
        Assert.Equal(400, state.Offset);
        Assert.False(state.ScrollBy(10));
        Assert.True(state.ScrollBy(-1000));
        Assert.Equal(0, state.Offset);
        Assert.False(state.ScrollBy(-5));
    }

    [Fact]
    public void Content_that_fits_cannot_be_scrolled()
    {
        var (state, _, _) = Make(80, 100);

        Assert.Equal(0, state.MaxOffset);
        Assert.False(state.ScrollBy(10));
        Assert.Equal(0, state.Offset);
    }

    [Fact]
    public void The_offset_is_limited_when_the_content_shrinks_and_comes_back_when_it_grows()
    {
        var (state, setExtent, _) = Make(500, 100);

        state.ScrollBy(400);
        setExtent(250);

        Assert.Equal(150, state.Offset);

        setExtent(500);

        Assert.Equal(400, state.Offset);

        setExtent(250);
        state.Clamp();
        setExtent(500);

        Assert.Equal(150, state.Offset);
    }

    [Fact]
    public void ScrollTo_goes_to_an_offset_within_the_limits()
    {
        var (state, _, _) = Make(500, 100);

        Assert.True(state.ScrollTo(120));
        Assert.Equal(120, state.Offset);
        Assert.False(state.ScrollTo(120));

        state.ScrollTo(9999);

        Assert.Equal(400, state.Offset);
    }

    [Fact]
    public void EnsureVisible_scrolls_the_least_that_shows_a_range_in_both_directions()
    {
        var (state, _, _) = Make(1000, 100);

        state.EnsureVisible(50, 20);

        Assert.Equal(0, state.Offset);

        state.EnsureVisible(180, 20);

        Assert.Equal(100, state.Offset);

        state.EnsureVisible(120, 20);

        Assert.Equal(100, state.Offset);

        state.EnsureVisible(40, 20);

        Assert.Equal(40, state.Offset);
    }

    [Fact]
    public void EnsureVisible_does_nothing_without_a_viewport_and_shows_the_end_of_a_range_that_is_too_long()
    {
        var (state, _, setViewport) = Make(1000, 0);

        state.EnsureVisible(300, 20);

        Assert.Equal(0, state.Offset);

        setViewport(100);
        state.EnsureVisible(300, 250);

        Assert.Equal(450, state.Offset);
    }

    [Fact]
    public void Reset_goes_back_to_the_start()
    {
        var (state, _, _) = Make(500, 100);

        state.ScrollBy(200);
        state.Reset();

        Assert.Equal(0, state.Offset);
    }
}
