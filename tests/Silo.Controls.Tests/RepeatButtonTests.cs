using Silo;
using Xunit;

public class RepeatButtonTests
{
    sealed class FakeClock : IAnimationClock
    {
        public TimeSpan Now { get; set; }
    }

    static (RepeatButton Button, FakeClock Clock, Box Root) Setup()
    {
        var clock = new FakeClock();
        var root = new Box { AnimationClock = clock };
        var button = new RepeatButton { Text = "+" };

        root.Children.Add(button);

        button.SetHovered(true);

        return (button, clock, root);
    }

    static void At(FakeClock clock, Box root, double milliseconds)
    {
        clock.Now = TimeSpan.FromMilliseconds(milliseconds);

        Animations.Tick(root);
    }

    [Fact]
    public void It_is_styled_as_a_Button()
    {
        Assert.Equal("Button", new RepeatButton().TypeName);
    }

    [Fact]
    public void Pressing_it_raises_Click_at_once_and_releasing_does_not()
    {
        var (button, _, _) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        button.PointerDown();

        Assert.Equal(1, clicks);
        Assert.True(button.IsRepeating);
        Assert.True(button.IsAnimating);

        button.PointerUp(true);

        Assert.Equal(1, clicks);
        Assert.False(button.IsRepeating);
        Assert.False(button.IsAnimating);
    }

    [Fact]
    public void It_waits_for_the_delay_and_then_repeats_every_interval()
    {
        var (button, clock, root) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;
        button.PointerDown();

        At(clock, root, 399);
        Assert.Equal(1, clicks);

        At(clock, root, 400);
        Assert.Equal(2, clicks);

        At(clock, root, 459);
        Assert.Equal(2, clicks);

        At(clock, root, 460);
        Assert.Equal(3, clicks);
    }

    [Fact]
    public void A_slow_frame_catches_up_with_every_missed_repeat()
    {
        var (button, clock, root) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;
        button.Delay = 100;
        button.Interval = 50;
        button.PointerDown();

        At(clock, root, 210);

        Assert.Equal(1 + 3, clicks);
    }

    [Fact]
    public void It_stops_after_release_and_pauses_while_the_pointer_is_outside()
    {
        var (button, clock, root) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;
        button.PointerDown();

        button.SetHovered(false);
        At(clock, root, 1000);
        Assert.Equal(1, clicks);

        button.SetHovered(true);
        At(clock, root, 1000);
        Assert.True(clicks > 1);

        var after = clicks;

        button.PointerUp(true);
        At(clock, root, 5000);

        Assert.Equal(after, clicks);
    }

    [Fact]
    public void Delay_and_Interval_can_be_changed()
    {
        var (button, clock, root) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;
        button.Delay = 10;
        button.Interval = 10;
        button.PointerDown();

        At(clock, root, 30);

        Assert.Equal(1 + 3, clicks);
    }
}
