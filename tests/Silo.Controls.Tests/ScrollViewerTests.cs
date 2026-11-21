using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class ScrollViewerTests
{
    static (Box Root, ScrollViewer Viewer, Button Last) Setup()
    {
        var column = new Box { Name = "column" };
        var viewer = new ScrollViewer { Name = "viewer" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var last = new Button { Text = "last" };

        for (var i = 0; i < 9; i++)
            column.Children.Add(new Button { Text = "item" });

        column.Children.Add(last);

        viewer.Content = column;
        root.Children.Add(viewer);

        var sheet = """{ "rules": { "Box": { "flex-direction": "column" }, "#viewer": { "height": "50px" }, "Button": { "height": "20px" } } }""";

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(200, 100));

        return (root, viewer, last);
    }

    [Fact]
    public void The_content_keeps_its_natural_height_and_the_viewport_stays_small()
    {
        var (_, viewer, _) = Setup();

        Assert.Equal(50, viewer.Bounds.Height);
        Assert.Equal(200, viewer.ContentExtent);
        Assert.Equal(150, viewer.MaxOffset);
    }

    [Fact]
    public void Scrolling_is_clamped_to_the_content()
    {
        var (_, viewer, _) = Setup();

        Assert.True(viewer.ScrollBy(40));
        Assert.Equal(40, viewer.Offset);

        Assert.True(viewer.ScrollBy(1000));
        Assert.Equal(150, viewer.Offset);

        Assert.False(viewer.ScrollBy(10));
        Assert.True(viewer.ScrollToTop());
        Assert.Equal(0, viewer.Offset);
    }

    [Fact]
    public void Hit_testing_follows_the_scrolled_content_and_ignores_the_clipped_part()
    {
        var (root, viewer, last) = Setup();

        Assert.NotSame(last, root.HitTest(5, 5));

        viewer.ScrollBy(1000);

        Assert.Same(last, root.HitTest(5, 45));
        Assert.Same(root, root.HitTest(5, 70));
    }

    [Fact]
    public void The_children_are_clipped_and_moved_when_painted()
    {
        var (root, viewer, _) = Setup();
        var builder = new DisplayListBuilder();

        viewer.ScrollBy(30);
        root.Render(builder);

        var commands = builder.Build().Commands;

        Assert.Contains(commands, c => c is ClipRectCommand { Rect: { Height: 50 } });
        Assert.Contains(commands, c => c is TranslateCommand { Dy: -30 });
    }

    [Fact]
    public void The_wheel_scrolls_the_viewer_under_the_pointer()
    {
        var (root, viewer, _) = Setup();
        var router = new PointerRouter(root);

        Assert.True(router.Scroll(5, 5, 40));
        Assert.Equal(40, viewer.Offset);
        Assert.False(router.Scroll(5, 90, 40));
    }
}
