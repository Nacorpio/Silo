using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class AnchoredPopupTests
{
    sealed class Probe : Box
    {
        public List<(Key Key, KeyModifiers Modifiers)> Keys { get; } = [];

        public List<string> Texts { get; } = [];

        public override bool KeyDown(Key key, KeyModifiers modifiers)
        {
            Keys.Add((key, modifiers));

            return true;
        }

        public override bool TextInput(string text)
        {
            Texts.Add(text);

            return true;
        }
    }

    const string Sheet = """
        {
          "rules": {
            "Box": { "padding": "10px", "align-items": "start" },
            "Anchor": { "width": "120px", "height": "30px" },
            "Probe": { "width": "50px", "height": "20px" }
          }
        }
        """;

    sealed class Anchor : Box;

    static (Anchor Anchor, AnchoredPopup Popup, OverlayLayer Layer, PointerRouter Router, StyleResolver Resolver) Setup(
        bool inWindow = true)
    {
        var anchor = new Anchor();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        if (inWindow)
            root.Overlays = layer;

        root.Children.Add(anchor);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        return (anchor, new AnchoredPopup(anchor), layer, new PointerRouter(root, layer), resolver);
    }

    static void Layout(OverlayLayer layer, StyleResolver resolver)
    {
        layer.ApplyStyles(resolver);
        layer.Arrange(new Size(400, 300));
    }

    [Fact]
    public void It_opens_right_below_the_left_edge_of_the_anchor()
    {
        var (anchor, popup, layer, _, resolver) = Setup();
        var content = new Probe();

        Assert.True(popup.Open(content));

        Layout(layer, resolver);

        Assert.True(popup.IsOpen);
        Assert.Same(content, popup.Content);
        Assert.Equal(anchor.Bounds.X, content.Bounds.X);
        Assert.Equal(anchor.Bounds.Bottom, content.Bounds.Y);
    }

    [Fact]
    public void Match_width_gives_the_content_the_width_of_the_anchor()
    {
        var (anchor, popup, layer, _, resolver) = Setup();
        var content = new Probe();

        popup.Open(content, matchWidth: true);

        Layout(layer, resolver);

        Assert.Equal(anchor.Bounds.Width, content.Bounds.Width);
    }

    [Fact]
    public void Without_match_width_the_content_keeps_its_own_width()
    {
        var (_, popup, layer, _, resolver) = Setup();
        var content = new Probe();

        popup.Open(content);

        Layout(layer, resolver);

        Assert.Equal(50, content.Bounds.Width);
    }

    [Fact]
    public void It_does_not_open_when_the_anchor_is_not_in_a_window()
    {
        var (_, popup, layer, _, _) = Setup(inWindow: false);

        Assert.False(popup.Open(new Probe()));
        Assert.False(popup.IsOpen);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void It_does_not_open_a_second_popup_while_one_is_open()
    {
        var (_, popup, layer, _, _) = Setup();

        popup.Open(new Probe());

        Assert.False(popup.Open(new Probe()));
        Assert.Equal(1, layer.Count);
    }

    [Fact]
    public void Close_closes_the_popup_and_reports_whether_it_was_open()
    {
        var (_, popup, layer, _, _) = Setup();

        popup.Open(new Probe());

        Assert.True(popup.Close());
        Assert.False(popup.IsOpen);
        Assert.Null(popup.Content);
        Assert.Equal(0, layer.Count);
        Assert.False(popup.Close());
    }

    [Fact]
    public void A_press_outside_closes_it_without_the_anchor_being_told()
    {
        var (_, popup, layer, router, resolver) = Setup();

        popup.Open(new Probe());

        Layout(layer, resolver);

        router.Down(350, 250);

        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Escape_closes_it()
    {
        var (_, popup, layer, _, _) = Setup();

        popup.Open(new Probe());

        Assert.True(layer.CloseTop());
        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Keys_and_text_go_to_the_open_content_and_nowhere_when_closed()
    {
        var (_, popup, _, _, _) = Setup();
        var content = new Probe();

        Assert.False(popup.KeyDown(Key.Enter, KeyModifiers.None));
        Assert.False(popup.TextInput("a"));

        popup.Open(content);

        Assert.True(popup.KeyDown(Key.Down, KeyModifiers.Shift));
        Assert.True(popup.TextInput("b"));
        Assert.Equal([(Key.Down, KeyModifiers.Shift)], content.Keys);
        Assert.Equal(["b"], content.Texts);
    }

    [Fact]
    public void Below_gives_the_position_under_a_control()
    {
        var (anchor, _, _, _, _) = Setup();

        var (x, y) = AnchoredPopup.Below(anchor);

        Assert.Equal(anchor.Bounds.X, x);
        Assert.Equal(anchor.Bounds.Bottom, y);
    }
}
