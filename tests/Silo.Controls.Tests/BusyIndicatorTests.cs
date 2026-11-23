using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class BusyIndicatorTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "align-items": "start" },
            "BusyIndicator": { "width": "200px", "height": "100px" },
            ".busy-overlay": { "flex-direction": "column", "align-items": "center", "justify-content": "center" },
            ".busy-ring": { "width": "20px", "height": "20px" }
          }
        }
        """;

    static (Box Root, BusyIndicator Busy, Button Inner, PointerRouter Router) Setup()
    {
        var inner = new Button { Text = "Save" };
        var busy = new BusyIndicator { Content = inner };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(busy);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        return (root, busy, inner, new PointerRouter(root));
    }

    static void Restyle(Box root)
    {
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));
    }

    [Fact]
    public void The_layer_is_only_in_the_tree_while_busy()
    {
        var (_, busy, inner, _) = Setup();

        Assert.Single(busy.Children);
        Assert.Same(inner, busy.Content);

        busy.IsBusy = true;

        Assert.Equal(2, busy.Children.Count);
        Assert.Same(busy.Overlay, busy.Children[1]);

        busy.IsBusy = false;

        Assert.Single(busy.Children);
    }

    [Fact]
    public void The_layer_covers_the_content_and_centers_the_ring()
    {
        var (root, busy, _, _) = Setup();

        busy.IsBusy = true;

        Restyle(root);

        Assert.Equal(new Rect(0, 0, 200, 100), busy.Overlay.Bounds);

        var ring = (Control)busy.Overlay.Children[0];

        Assert.Equal(90, ring.Bounds.X, 3);
        Assert.Equal(40, ring.Bounds.Y, 3);
    }

    [Fact]
    public void A_press_on_the_content_is_blocked_while_busy()
    {
        var (root, busy, inner, router) = Setup();
        var clicks = 0;

        inner.Click += (_, _) => clicks++;

        router.Move(5, 5);
        router.Down(5, 5);
        router.Up(5, 5);

        Assert.Equal(1, clicks);

        busy.IsBusy = true;

        Restyle(root);

        router.Move(6, 6);
        router.Down(6, 6);
        router.Up(6, 6);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void The_message_is_shown_only_when_it_is_not_empty()
    {
        var (_, busy, _, _) = Setup();

        Assert.Single(busy.Overlay.Children);

        busy.Message = "Loading";

        Assert.Equal(2, busy.Overlay.Children.Count);
        Assert.Equal("Loading", ((TextBlock)busy.Overlay.Children[1]).Text);

        busy.Message = string.Empty;

        Assert.Single(busy.Overlay.Children);
    }

    [Fact]
    public void Replacing_the_content_keeps_the_layer_on_top()
    {
        var (_, busy, _, _) = Setup();

        busy.IsBusy = true;
        busy.Content = new Box();

        Assert.Equal(2, busy.Children.Count);
        Assert.Same(busy.Overlay, busy.Children[1]);
    }
}
