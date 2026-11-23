using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ScrollBarTests
{
    static (Box Root, ScrollBar Bar, PointerRouter Router) Setup()
    {
        var bar = new ScrollBar { Maximum = 300, ViewportSize = 100 };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "align-items": "start" }, "ScrollBar": { "width": "12px", "height": "200px" } } }""";

        root.Children.Add(bar);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(100, 300));

        return (root, bar, new PointerRouter(root));
    }

    [Fact]
    public void The_thumb_is_as_long_as_the_visible_share_and_moves_with_the_value()
    {
        var (_, bar, _) = Setup();

        Assert.Equal(100 * 200 / 400, bar.ThumbLength);
        Assert.Equal(0, bar.ThumbStart);

        bar.Value = 300;

        Assert.Equal(200 - bar.ThumbLength, bar.ThumbStart);
    }

    [Fact]
    public void The_thumb_is_never_shorter_than_the_minimum_and_fills_the_track_when_all_fits()
    {
        var (_, bar, _) = Setup();

        bar.ViewportSize = 1;

        Assert.Equal(24, bar.ThumbLength);

        bar.Maximum = 0;

        Assert.Equal(200, bar.ThumbLength);
    }

    [Fact]
    public void The_value_is_clamped_and_the_event_is_raised_only_on_change()
    {
        var bar = new ScrollBar { Maximum = 50 };
        var raised = 0;

        bar.ValueChanged += (_, _) => raised++;

        bar.Value = 99;
        bar.Value = 50;

        Assert.Equal(50, bar.Value);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Dragging_the_thumb_scrolls_in_proportion_to_the_track()
    {
        var (_, bar, router) = Setup();

        router.Move(5, 10);
        router.Down(5, 10);
        router.Move(5, 10 + 50);

        Assert.Equal(50 / (200 - bar.ThumbLength) * 300, bar.Value, 6);

        router.Move(5, 900);

        Assert.Equal(300, bar.Value);

        router.Up(5, 900);
        router.Move(5, 0);

        Assert.Equal(300, bar.Value);
    }

    [Fact]
    public void Pressing_the_track_moves_by_a_page_toward_the_pointer()
    {
        var (_, bar, router) = Setup();

        router.Move(5, 190);
        router.Down(5, 190);
        router.Up(5, 190);

        Assert.Equal(100, bar.Value);

        router.Move(5, 190);
        router.Down(5, 190);
        router.Up(5, 190);
        router.Move(5, 1);
        router.Down(5, 1);

        Assert.Equal(100, bar.Value);
    }

    [Fact]
    public void The_wheel_over_the_bar_scrolls_it()
    {
        var (_, bar, router) = Setup();

        Assert.True(router.Scroll(5, 100, 48));
        Assert.Equal(48, bar.Value);
    }

    [Fact]
    public void A_horizontal_bar_uses_the_x_position()
    {
        var bar = new ScrollBar { Orientation = Orientation.Horizontal, Maximum = 300, ViewportSize = 100 };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "align-items": "start" }, "ScrollBar": { "width": "200px", "height": "12px" } } }""";

        root.Children.Add(bar);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        var router = new PointerRouter(root);

        router.Move(190, 5);
        router.Down(190, 5);

        Assert.Equal(100, bar.Value);
    }
}
