using Silo;
using Xunit;

public class CarouselTests
{
    static Carousel Create(bool looping = false)
        => new() { Items = [new TextBlock { Text = "A" }, new TextBlock { Text = "B" }, new TextBlock { Text = "C" }], IsLooping = looping };

    static Control Viewport(Carousel carousel) => (Control)((Control)carousel.Children[0]).Children[1];

    static Control Dots(Carousel carousel) => (Control)carousel.Children[1];

    static void Click(Carousel carousel, int stageIndex)
    {
        var button = (Button)((Control)carousel.Children[0]).Children[stageIndex];

        button.PointerDown();
        button.PointerUp(inside: true);
    }

    [Fact]
    public void The_first_item_is_shown_with_a_dot_for_each_item()
    {
        var carousel = Create();

        Assert.Equal("A", ((TextBlock)Viewport(carousel).Children.Single()).Text);
        Assert.Equal(3, Dots(carousel).Children.Count);
        Assert.Contains("current", ((Control)Dots(carousel).Children[0]).Classes);
    }

    [Fact]
    public void The_buttons_move_the_item_and_stop_at_the_ends_without_looping()
    {
        var carousel = Create();

        Click(carousel, 0);

        Assert.Equal(0, carousel.SelectedIndex);

        Click(carousel, 2);
        Click(carousel, 2);
        Click(carousel, 2);

        Assert.Equal(2, carousel.SelectedIndex);
        Assert.Equal("C", ((TextBlock)Viewport(carousel).Children.Single()).Text);
    }

    [Fact]
    public void Looping_wraps_around_in_both_directions()
    {
        var carousel = Create(looping: true);

        Click(carousel, 0);

        Assert.Equal(2, carousel.SelectedIndex);

        Click(carousel, 2);

        Assert.Equal(0, carousel.SelectedIndex);
    }

    [Fact]
    public void A_dot_selects_its_item_and_the_event_is_raised_per_change()
    {
        var carousel = Create();
        var raised = 0;

        carousel.SelectionChanged += (_, _) => raised++;

        var dot = (Button)Dots(carousel).Children[2];

        dot.PointerDown();
        dot.PointerUp(inside: true);

        Assert.Equal(2, carousel.SelectedIndex);
        Assert.Equal(1, raised);
        Assert.Contains("current", ((Control)Dots(carousel).Children[2]).Classes);
    }

    [Fact]
    public void The_keys_move_the_item()
    {
        var carousel = Create();

        Assert.True(carousel.KeyDown(Key.Right));
        Assert.True(carousel.KeyDown(Key.End));
        Assert.Equal(2, carousel.SelectedIndex);
        Assert.False(carousel.KeyDown(Key.Right));
        Assert.True(carousel.KeyDown(Key.Home));
    }

    [Fact]
    public void An_empty_carousel_shows_nothing_and_ignores_the_buttons()
    {
        var carousel = new Carousel();

        Click(carousel, 2);

        Assert.Equal(-1, carousel.SelectedIndex);
        Assert.Empty(Viewport(carousel).Children);
        Assert.Empty(Dots(carousel).Children);
    }
}
