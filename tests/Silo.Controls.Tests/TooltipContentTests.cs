using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class TooltipContentTests
{
    sealed record Picture(int Width, int Height) : Silo.Rendering.IImage;

    const string Sheet = """
        { "rules": {
          "Box": { "align-items": "start" },
          "Tooltip": { "flex-direction": "row", "gap": "10px", "padding": "4px" },
          "Tooltip .body": { "flex-direction": "column" },
          "Tooltip .header-row": { "flex-direction": "row", "gap": "10px" },
          "Tooltip .text-row": { "flex-direction": "row", "gap": "10px" },
          "Tooltip .icon": { "width": "24px", "height": "24px" },
          "Tooltip .description": { "max-width": "70px" }
        } }
        """;

    static Tooltip Arrange(Tooltip tip)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(tip);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        return tip;
    }

    [Fact]
    public void Without_extras_the_tooltip_stays_a_row_of_text_and_shortcut()
    {
        var tip = new Tooltip { Text = "Save", Shortcut = "Ctrl+S" };

        Assert.Equal(2, tip.Children.Count);
        Assert.IsType<TextBlock>(tip.Children[0]);
    }

    [Fact]
    public void A_header_goes_above_the_text_with_the_shortcut_on_its_line()
    {
        var tip = Arrange(new Tooltip { Header = "Save", Text = "Write to disk", Shortcut = "Ctrl+S" });
        var body = (Control)tip.Children.Single();
        var headerRow = (Control)body.Children[0];
        var text = (Control)((Control)body.Children[1]).Children[0];

        Assert.Equal(2, headerRow.Children.Count);
        Assert.Contains("header", ((Control)headerRow.Children[0]).Classes);
        Assert.Contains("shortcut", ((Control)headerRow.Children[1]).Classes);
        Assert.Equal("Write to disk", ((TextBlock)text).Text);
        Assert.True(headerRow.Bounds.Y < text.Bounds.Y + 1);
    }

    [Fact]
    public void The_icon_is_on_the_left_of_the_body_and_a_picture_wins_over_a_glyph()
    {
        var tip = Arrange(new Tooltip { Text = "Hi", Glyph = "check" });

        Assert.IsType<Icon>(tip.Children[0]);
        Assert.True(tip.Children[0] is Control icon && icon.Bounds.X < ((Control)tip.Children[1]).Bounds.X);

        tip.Icon = new Picture(8, 8);

        Assert.IsType<Silo.Image>(tip.Children[0]);
    }

    [Fact]
    public void The_description_wraps_at_its_max_width_and_the_tooltip_grows_taller()
    {
        var plain = Arrange(new Tooltip { Header = "H", Text = "T" });
        var wrapped = Arrange(new Tooltip { Header = "H", Text = "T", Description = "one two three four five six" });

        // 70px holds ten characters, so the description needs more than two lines of 14px.
        Assert.True(wrapped.Bounds.Height >= plain.Bounds.Height + (3 * 14));
        Assert.True(wrapped.Bounds.Width <= 4 + 4 + 70 + 1);
    }

    [Fact]
    public void Content_replaces_every_other_part()
    {
        var content = new Box();
        var tip = new Tooltip { Text = "x", Header = "h", Content = content };

        Assert.Single(tip.Children);
        Assert.Same(content, tip.Children[0]);

        tip.Content = null;

        Assert.NotEmpty(tip.Children);
    }

    [Fact]
    public void The_presenter_shows_the_new_parts_and_reuses_the_content_after_a_dismiss()
    {
        var content = new Box();
        var button = new Button { TooltipHeader = "Save", TooltipDescription = "Long text", TooltipGlyph = "check", TooltipContent = null };
        var withContent = new Button { TooltipContent = content };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var tooltips = new TooltipPresenter(layer);
        var start = TimeSpan.FromSeconds(1);

        root.Children.Add(button);
        root.Children.Add(withContent);

        tooltips.Hover(button, 1, 1, start);
        tooltips.Tick(start + TimeSpan.FromSeconds(1));

        Assert.Equal("Save", tooltips.Current!.Header);
        Assert.Equal("Long text", tooltips.Current.Description);
        Assert.Equal("check", tooltips.Current.Glyph);

        tooltips.Dismiss();
        tooltips.Hover(withContent, 1, 1, start);
        tooltips.Tick(start + TimeSpan.FromSeconds(1));
        tooltips.Dismiss();
        tooltips.Hover(withContent, 1, 1, start);

        Assert.True(tooltips.Tick(start + TimeSpan.FromSeconds(1)));
        Assert.Same(content, tooltips.Current!.Children[0]);
    }
}
