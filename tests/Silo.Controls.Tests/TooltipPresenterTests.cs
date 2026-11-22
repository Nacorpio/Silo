using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class TooltipPresenterTests
{
    static readonly TimeSpan Start = TimeSpan.FromSeconds(10);

    static (Box Root, Button Button, Box Plain, OverlayLayer Layer, TooltipPresenter Tooltips) Setup()
    {
        var button = new Button { Text = "Save", Tooltip = "Save the document" };
        var plain = new Box { Name = "plain" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var sheet = """{ "rules": { "Box": { "align-items": "start" }, "#plain": { "width": "50px", "height": "50px" } } }""";

        root.Overlays = layer;
        root.Children.Add(button);
        root.Children.Add(plain);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        return (root, button, plain, layer, new TooltipPresenter(layer));
    }

    static TimeSpan After(int milliseconds) => Start + TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void The_tooltip_shows_once_the_pointer_has_rested_for_the_delay_and_not_before()
    {
        var (_, button, _, layer, tooltips) = Setup();

        tooltips.Hover(button, 10, 10, Start);

        Assert.False(tooltips.Tick(After(499)));
        Assert.False(tooltips.IsShown);
        Assert.True(tooltips.Tick(After(500)));
        Assert.True(tooltips.IsShown);
        Assert.Equal(1, layer.Count);
        Assert.Equal(0, layer.ModalCount);
        Assert.False(tooltips.Tick(After(900)));
    }

    [Fact]
    public void It_is_placed_below_and_right_of_the_pointer_with_the_text_of_the_Control()
    {
        var (_, button, _, layer, tooltips) = Setup();

        tooltips.Hover(button, 10, 10, Start);
        tooltips.Tick(After(500));
        layer.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("{}", "t.jsonc")]));
        layer.Arrange(new Size(300, 200));

        var tip = tooltips.Current!;

        Assert.Equal("Save the document", tip.Text);
        Assert.Equal((22, 30), (tip.Bounds.X, tip.Bounds.Y));
    }

    [Fact]
    public void A_Control_inside_one_with_a_tooltip_uses_it_and_one_without_shows_nothing()
    {
        var (_, button, plain, layer, tooltips) = Setup();
        var inner = new Box();

        button.Children.Add(inner);
        tooltips.Hover(inner, 5, 5, Start);

        Assert.True(tooltips.Tick(After(500)));

        tooltips.Dismiss();
        tooltips.Hover(plain, 5, 5, After(1000));

        Assert.False(tooltips.Tick(After(2000)));
        Assert.Equal(0, layer.Count);
        Assert.Null(tooltips.DueIn(After(2000)));
    }

    [Fact]
    public void Moving_to_another_Control_hides_the_tooltip_and_restarts_the_delay()
    {
        var (_, button, _, layer, tooltips) = Setup();
        var other = new Button { Tooltip = "Other" };

        tooltips.Hover(button, 10, 10, Start);
        tooltips.Tick(After(500));

        Assert.True(tooltips.Hover(other, 90, 10, After(600)));
        Assert.Equal(0, layer.Count);
        Assert.False(tooltips.Tick(After(900)));
        Assert.Equal(TimeSpan.FromMilliseconds(200), tooltips.DueIn(After(900)));
        Assert.True(tooltips.Tick(After(1100)));
    }

    [Fact]
    public void Moving_inside_the_same_Control_keeps_the_tooltip_and_the_timer()
    {
        var (_, button, _, layer, tooltips) = Setup();

        tooltips.Hover(button, 10, 10, Start);

        Assert.False(tooltips.Hover(button, 14, 12, After(300)));
        Assert.True(tooltips.Tick(After(500)));
        Assert.False(tooltips.Hover(button, 18, 14, After(700)));
        Assert.Equal(1, layer.Count);
    }

    [Fact]
    public void Dismiss_hides_the_tooltip_and_nothing_shows_until_the_pointer_comes_to_a_Control_again()
    {
        var (_, button, _, layer, tooltips) = Setup();

        tooltips.Hover(button, 10, 10, Start);
        tooltips.Tick(After(500));

        Assert.True(tooltips.Dismiss());
        Assert.False(tooltips.Dismiss());
        Assert.Equal(0, layer.Count);
        Assert.False(tooltips.Tick(After(5000)));

        tooltips.Hover(button, 11, 10, After(6000));

        Assert.True(tooltips.Tick(After(6500)));
    }

    [Fact]
    public void No_tooltip_shows_while_a_modal_overlay_is_open()
    {
        var (_, button, _, layer, tooltips) = Setup();

        layer.Show(new Box(), 100, 100);
        tooltips.Hover(button, 10, 10, Start);

        Assert.False(tooltips.Tick(After(2000)));
    }

    [Fact]
    public void The_delay_can_be_changed()
    {
        var (_, button, _, _, tooltips) = Setup();

        tooltips.Delay = TimeSpan.FromMilliseconds(100);
        tooltips.Hover(button, 10, 10, Start);

        Assert.True(tooltips.Tick(After(100)));
    }

    [Fact]
    public void The_shortcut_is_shown_after_the_text_as_a_part_with_the_shortcut_class()
    {
        var (_, button, _, layer, tooltips) = Setup();

        button.TooltipShortcut = "Ctrl+S";
        tooltips.Hover(button, 10, 10, Start);
        tooltips.Tick(After(500));

        var tip = tooltips.Current!;

        Assert.Equal("Ctrl+S", tip.Shortcut);
        Assert.Equal(2, tip.Children.Count);

        var keys = Assert.IsType<TextBlock>(tip.Children[1]);

        Assert.Equal("Ctrl+S", keys.Text);
        Assert.Contains("shortcut", keys.Classes);
        Assert.Equal(1, layer.Count);
    }

    [Fact]
    public void Without_a_shortcut_the_tooltip_is_only_the_text_and_clearing_it_removes_the_part()
    {
        var tip = new Tooltip { Text = "Save", Shortcut = "Ctrl+S" };

        Assert.Equal(2, tip.Children.Count);

        tip.Shortcut = string.Empty;

        Assert.Single(tip.Children);

        tip.Shortcut = "Ctrl+S";
        tip.Shortcut = "Ctrl+Shift+S";

        Assert.Equal(2, tip.Children.Count);
    }
}
