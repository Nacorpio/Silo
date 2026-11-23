using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class ScrollViewerBarTests
{
    static (Box Root, ScrollViewer Viewer, StyleResolver Styles) Setup(string viewerRule = "")
    {
        var column = new Box { Name = "column" };
        var viewer = new ScrollViewer { Name = "viewer" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        for (var i = 0; i < 10; i++)
            column.Children.Add(new Button { Text = "item" });

        viewer.Content = column;
        root.Children.Add(viewer);

        var sheet = $$"""{ "rules": { "Box": { "flex-direction": "column" }, "#viewer": { "height": "50px"{{viewerRule}} }, "Button": { "height": "20px" } } }""";
        var styles = new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]);

        root.ApplyStyles(styles);
        root.Arrange(new Size(200, 100));

        return (root, viewer, styles);
    }

    static void Relayout(Box root, StyleResolver styles)
    {
        root.ApplyStyles(styles);
        root.Arrange(new Size(200, 100));
        root.Render(new DisplayListBuilder());
    }

    [Fact]
    public void The_bar_is_hidden_until_the_pointer_is_over_the_viewer()
    {
        var (root, viewer, styles) = Setup();

        Assert.False(viewer.IsScrollBarVisible);
        Assert.Equal(0, viewer.ScrollBar.Bounds.Width);

        viewer.SetHovered(true);
        Relayout(root, styles);

        Assert.True(viewer.IsScrollBarVisible);
        Assert.Equal(12, viewer.ScrollBar.Bounds.Width);
        Assert.Equal(188, viewer.ScrollBar.Bounds.X);
        Assert.Equal(50, viewer.ScrollBar.Bounds.Height);
    }

    [Fact]
    public void Always_shows_the_bar_and_never_hides_it()
    {
        var (_, always, _) = Setup(", \"scrollbar-visibility\": \"always\"");
        var (rootNever, never, stylesNever) = Setup(", \"scrollbar-visibility\": \"never\"");

        never.SetHovered(true);
        Relayout(rootNever, stylesNever);

        Assert.True(always.IsScrollBarVisible);
        Assert.False(never.IsScrollBarVisible);
    }

    [Fact]
    public void The_bar_is_hidden_when_everything_fits()
    {
        var viewer = new ScrollViewer { Name = "viewer" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        viewer.Content = new Button { Text = "one" };
        root.Children.Add(viewer);
        root.Arrange(new Size(200, 100));
        viewer.SetHovered(true);

        Assert.Equal(0, viewer.MaxOffset);
        Assert.False(viewer.IsScrollBarVisible);
    }

    [Fact]
    public void The_bar_follows_the_scroll_position_and_extent()
    {
        var (root, viewer, styles) = Setup();

        viewer.SetHovered(true);
        Relayout(root, styles);

        Assert.Equal(150, viewer.ScrollBar.Maximum);
        Assert.Equal(50, viewer.ScrollBar.ViewportSize);

        viewer.ScrollBy(40);

        Assert.Equal(40, viewer.ScrollBar.Value);
        Assert.Equal(40, viewer.Offset);
    }

    [Fact]
    public void Dragging_the_thumb_scrolls_the_content_and_the_bar_does_not_move_with_it()
    {
        var (root, viewer, styles) = Setup();
        var router = new PointerRouter(root, new OverlayLayer { TextMeasurer = root.TextMeasurer });

        router.Move(194, 5);
        Relayout(root, styles);
        router.Down(194, 5);
        router.Move(194, 30);

        Assert.True(viewer.Offset > 0);

        var (x, y) = viewer.ScrollBar.ToLocal(0, 0);

        Assert.Equal(-188, x);
        Assert.Equal(0, y);

        router.Up(194, 30);
    }

    [Fact]
    public void Pressing_the_content_area_does_not_hit_the_hidden_bar()
    {
        var (root, viewer, _) = Setup();

        var hit = root.HitTest(194, 5);

        Assert.NotSame(viewer.ScrollBar, hit);
        Assert.IsType<Button>(hit);
    }

    [Fact]
    public void Replacing_the_content_keeps_the_bar_and_puts_the_new_content_first()
    {
        var viewer = new ScrollViewer();
        var first = new Box();
        var second = new Box();

        viewer.Content = first;
        viewer.Content = second;

        Assert.Equal(2, viewer.Children.Count);
        Assert.Same(second, viewer.Children[0]);
        Assert.Same(viewer.ScrollBar, viewer.Children[1]);

        viewer.Content = null;

        Assert.Single(viewer.Children);
    }
}
