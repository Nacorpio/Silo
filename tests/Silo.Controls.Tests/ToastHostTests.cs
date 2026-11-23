using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ToastHostTests
{
    const string Sheet = """
        {
          "rules": {
            "ToastHost": { "flex-direction": "column", "gap": "10px", "padding": "10px" },
            "Toast": { "flex-direction": "row", "width": "200px", "height": "40px" }
          }
        }
        """;

    static (Box Root, OverlayLayer Layer, ToastHost Host, StyleResolver Resolver, PointerRouter Router) Setup()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        root.Overlays = layer;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(600, 400));

        return (root, layer, new ToastHost(layer), resolver, new PointerRouter(root, layer));
    }

    static void Layout(OverlayLayer layer, StyleResolver resolver)
    {
        layer.ApplyStyles(resolver);
        layer.Arrange(new Size(600, 400));
    }

    [Fact]
    public void Showing_a_toast_shows_the_host_once_in_the_overlay_layer()
    {
        var (_, layer, host, _, _) = Setup();

        host.Show("One");
        host.Show("Two");

        Assert.Equal(1, layer.Count);
        Assert.Equal(0, layer.ModalCount);
        Assert.Equal(2, host.Toasts.Count);
        Assert.Equal("One", host.Toasts[0].Message);
    }

    [Theory]
    [InlineData(ToastPosition.BottomRight, true, true)]
    [InlineData(ToastPosition.BottomLeft, false, true)]
    [InlineData(ToastPosition.TopRight, true, false)]
    [InlineData(ToastPosition.TopLeft, false, false)]
    public void The_toasts_stand_in_the_chosen_corner(ToastPosition position, bool right, bool bottom)
    {
        var (_, layer, host, resolver, _) = Setup();

        host.Position = position;

        var toast = host.Show("Hi");

        Layout(layer, resolver);

        var bounds = toast.Bounds;
        var (x, y) = toast.ToLocal(0, 0);

        Assert.Equal(right, -x > 300);
        Assert.Equal(bottom, -y > 200);
        Assert.Equal(200, bounds.Width);
    }

    [Fact]
    public void The_newest_toast_is_nearest_to_the_corner()
    {
        var (_, layer, host, resolver, _) = Setup();

        var first = host.Show("First");
        var second = host.Show("Second");

        Layout(layer, resolver);

        Assert.True(ScreenY(second) > ScreenY(first));

        host.Position = ToastPosition.TopRight;

        Layout(layer, resolver);

        Assert.True(ScreenY(second) < ScreenY(first));
    }

    [Fact]
    public void More_than_the_maximum_dismisses_the_oldest()
    {
        var (_, _, host, _, _) = Setup();
        var closed = new List<string>();

        host.MaxCount = 2;

        foreach (var name in new[] { "A", "B", "C" })
            host.Show(name).Closed += (_, _) => closed.Add(name);

        host.Show("D");

        Assert.Equal(["A", "B"], closed);
        Assert.Equal(["C", "D"], host.Toasts.Select(t => t.Message));
    }

    [Fact]
    public void A_toast_is_dismissed_when_its_duration_has_passed()
    {
        var (_, layer, host, _, _) = Setup();
        var closed = 0;

        host.Duration = TimeSpan.FromSeconds(4);

        var toast = host.Show("Hi");

        toast.Closed += (_, _) => closed++;

        Assert.False(host.Tick(TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(4), host.DueIn());

        Assert.False(host.Tick(TimeSpan.FromSeconds(13)));
        Assert.True(host.Tick(TimeSpan.FromSeconds(14)));

        Assert.Empty(host.Toasts);
        Assert.Equal(1, closed);
        Assert.Equal(0, layer.Count);
        Assert.Null(host.DueIn());
    }

    [Fact]
    public void A_zero_duration_keeps_the_toast_until_it_is_dismissed()
    {
        var (_, _, host, _, _) = Setup();

        host.Show("Stays", duration: TimeSpan.Zero);

        host.Tick(TimeSpan.Zero);

        Assert.False(host.Tick(TimeSpan.FromHours(1)));
        Assert.Null(host.DueIn());
        Assert.Single(host.Toasts);
    }

    [Fact]
    public void The_timer_stops_while_the_pointer_is_over_the_toast()
    {
        var (_, _, host, _, _) = Setup();
        var toast = host.Show("Hi", duration: TimeSpan.FromSeconds(4));

        host.Tick(TimeSpan.Zero);
        host.Tick(TimeSpan.FromSeconds(3));

        toast.SetHovered(true);

        Assert.False(host.Tick(TimeSpan.FromSeconds(30)));
        Assert.Null(host.DueIn());

        toast.SetHovered(false);

        Assert.False(host.Tick(TimeSpan.FromSeconds(30.5)));
        Assert.True(host.Tick(TimeSpan.FromSeconds(31.1)));
    }

    [Fact]
    public void Pressing_the_close_button_dismisses_the_toast()
    {
        var (_, layer, host, resolver, router) = Setup();
        var toast = host.Show("Hi");

        Layout(layer, resolver);

        var close = toast.Children.OfType<Button>().Last();
        var (cx, cy) = close.ToLocal(0, 0);

        Assert.True(router.Down(-cx + 2, -cy + 2));
        router.Up(-cx + 2, -cy + 2);

        Assert.Empty(host.Toasts);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void The_action_button_raises_the_event_and_dismisses_the_toast()
    {
        var (_, layer, host, resolver, router) = Setup();
        var toast = host.Show("Hi", actionText: "Undo");
        var invoked = 0;

        toast.ActionInvoked += (_, _) => invoked++;

        Layout(layer, resolver);

        var action = toast.Children.OfType<Button>().First();
        var (ax, ay) = action.ToLocal(0, 0);

        router.Down(-ax + 2, -ay + 2);
        router.Up(-ax + 2, -ay + 2);

        Assert.Equal(1, invoked);
        Assert.Empty(host.Toasts);
    }

    [Fact]
    public void A_press_beside_the_toasts_goes_to_what_is_below()
    {
        var (root, layer, host, resolver, router) = Setup();
        var button = new Button { Text = "Under" };
        var clicks = 0;

        root.Children.Add(button);
        button.Click += (_, _) => clicks++;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(600, 400));

        var toast = host.Show("Hi");

        Layout(layer, resolver);

        Assert.NotNull(layer.HitTest(-toast.ToLocal(0, 0).X + 5, -toast.ToLocal(0, 0).Y + 5));
        Assert.Null(layer.HitTest(5, 5));
        Assert.Equal(1, layer.Count);

        router.Down(button.Bounds.X + 2, button.Bounds.Y + 2);
        router.Up(button.Bounds.X + 2, button.Bounds.Y + 2);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void The_severity_is_a_class_and_the_parts_follow_what_is_set()
    {
        var (_, _, host, _, _) = Setup();
        var toast = host.Show("Hi", "Title", Severity.Error, new FakeImage(8, 8), "Retry");

        Assert.Contains("error", toast.Classes);
        Assert.DoesNotContain("info", toast.Classes);
        Assert.Equal(4, toast.Children.Count);

        toast.ActionText = string.Empty;
        toast.Icon = null;

        Assert.Equal(2, toast.Children.Count);
    }

    [Fact]
    public void Dismiss_all_closes_every_toast_and_the_host()
    {
        var (_, layer, host, _, _) = Setup();

        host.Show("A");
        host.Show("B");

        Assert.True(host.DismissAll());
        Assert.False(host.DismissAll());
        Assert.Equal(0, layer.Count);
    }

    static double ScreenY(Toast toast) => -toast.ToLocal(0, 0).Y;
}
