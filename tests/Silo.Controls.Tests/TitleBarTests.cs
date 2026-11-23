using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class TitleBarTests
{
    sealed class FakeHost : IWindowHost
    {
        public bool IsMaximized { get; set; }

        public bool AlwaysOnTop { get; set; }

        public List<string> Calls { get; } = [];

        public void Minimize() => Calls.Add("minimize");

        public void Maximize()
        {
            Calls.Add("maximize");

            IsMaximized = true;
        }

        public void Restore()
        {
            Calls.Add("restore");

            IsMaximized = false;
        }

        public void Close() => Calls.Add("close");
    }

    static (Window Window, TitleBar Bar, FakeHost Host) Setup(string bar = "")
    {
        var window = new Window { TextMeasurer = new FixedMeasurer() };
        var titleBar = new TitleBar { Title = "Notes", Name = "bar" };
        var host = new FakeHost();

        window.Host = host;
        window.Children.Add(titleBar);

        var sheet = $$"""
            { "rules": {
              "Window": { "flex-direction": "column" },
              "TitleBar": { "flex-direction": "row", "align-items": "center", "height": "30px" {{bar}} },
              ".content": { "flex-grow": "1", "flex-direction": "row" },
              "TitleBarButton": { "width": "40px", "height": "30px", "flex-shrink": "0" }
            } }
            """;

        window.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        window.Arrange(new Size(400, 200));

        return (window, titleBar, host);
    }

    static void Click(Button button)
    {
        button.PointerDown();
        button.PointerUp(inside: true);
    }

    [Fact]
    public void The_buttons_ask_the_window_to_minimize_maximize_restore_and_close()
    {
        var (window, bar, host) = Setup();

        Click(bar.MinimizeButton);
        Click(bar.MaximizeButton);

        Assert.True(window.IsMaximized);

        Click(bar.MaximizeButton);
        Click(bar.CloseButton);

        Assert.Equal(["minimize", "maximize", "restore", "close"], host.Calls);
    }

    [Fact]
    public void The_maximize_button_shows_the_restore_glyph_while_the_window_is_maximized()
    {
        var (window, bar, host) = Setup();

        Assert.Equal("maximize", ((TitleBarButton)bar.MaximizeButton).Glyph);

        host.IsMaximized = true;
        window.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": {} }""", "t.jsonc")]));

        Assert.Equal("restore", ((TitleBarButton)bar.MaximizeButton).Glyph);
    }

    [Fact]
    public void The_buttons_are_on_the_right_by_default_and_on_the_left_when_styled()
    {
        var (_, bar, _) = Setup("""  """);

        Assert.True(bar.CloseButton.Bounds.X > bar.Children.OfType<TextBlock>().Single().Bounds.X || OperatingSystem.IsMacOS());

        var (_, left, _) = Setup(""", "button-position": "left" """);

        Assert.True(left.CloseButton.Bounds.X < left.Children.OfType<TextBlock>().Single().Bounds.X);
    }

    [Fact]
    public void The_empty_bar_title_and_icon_drag_but_the_buttons_and_the_content_do_not()
    {
        var (window, bar, _) = Setup();
        var menu = new Button { Text = "File" };

        bar.Content = menu;
        window.Arrange(new Size(400, 200));

        var title = bar.Children.OfType<TextBlock>().Single();

        Assert.Equal(WindowRegion.Drag, WindowHitTester.Test(window, title.Bounds.X + 8, 15, 400, 200, true));
        Assert.Equal(WindowRegion.Client, WindowHitTester.Test(window, bar.CloseButton.Bounds.X + 5, 15, 400, 200, true));
        Assert.Equal(WindowRegion.Client, WindowHitTester.Test(window, title.Bounds.Right + 5, 15, 400, 200, true));
    }

    [Fact]
    public void The_edges_of_a_resizable_window_resize_and_a_maximized_window_does_not()
    {
        var (window, _, _) = Setup();

        Assert.Equal(WindowRegion.ResizeTopLeft, WindowHitTester.Test(window, 1, 1, 400, 200, true));
        Assert.Equal(WindowRegion.ResizeBottom, WindowHitTester.Test(window, 200, 198, 400, 200, true));
        Assert.Equal(WindowRegion.ResizeRight, WindowHitTester.Test(window, 399, 100, 400, 200, true));
        Assert.NotEqual(WindowRegion.ResizeTopLeft, WindowHitTester.Test(window, 1, 1, 400, 200, false));
    }

    [Fact]
    public void A_Control_can_be_marked_as_a_drag_area()
    {
        var (window, bar, _) = Setup();
        var strip = new Box { IsWindowDragArea = true };

        bar.Content = strip;

        Assert.True(strip.IsWindowDragArea);
        Assert.Equal(WindowRegion.Client, WindowHitTester.Test(window, 200, 100, 400, 200, true));
    }
}
