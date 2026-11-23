using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class IconTests
{
    const string Sheet = """{ "rules": { "Box": { "align-items": "start" }, "Icon": { "icon-size": "24px", "color": "#336699", "icon-stroke": "3px" } } }""";

    static (Box Root, Icon Icon) Setup(string glyph)
    {
        var icon = new Icon { Glyph = glyph };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(icon);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(200, 100));

        return (root, icon);
    }

    static List<DrawCommand> Frame(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands];
    }

    [Fact]
    public void It_is_a_square_of_the_icon_size()
    {
        var (_, icon) = Setup("check");

        Assert.Equal(24, icon.Bounds.Width);
        Assert.Equal(24, icon.Bounds.Height);
    }

    [Fact]
    public void A_glyph_is_drawn_in_the_text_color_with_the_stroke_width()
    {
        var (root, _) = Setup("close");
        var lines = Frame(root).OfType<StrokePolylineCommand>().ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(new Color(51, 102, 153), l.Color));
        Assert.All(lines, l => Assert.Equal(3, l.Width));
    }

    [Fact]
    public void A_closed_shape_is_drawn_closed_and_scaled_to_the_icon()
    {
        var (root, _) = Setup("star");
        var star = Frame(root).OfType<StrokePolylineCommand>().Single();

        Assert.True(star.Closed);
        Assert.Equal(10, star.Points.Count);
        Assert.All(star.Points, p => Assert.InRange(p.X, 0, 24));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-such-glyph")]
    public void An_unknown_glyph_draws_nothing(string glyph)
    {
        var (root, _) = Setup(glyph);

        Assert.DoesNotContain(Frame(root), c => c is StrokePolylineCommand);
    }

    [Fact]
    public void Every_listed_name_has_a_glyph_with_lines_inside_the_grid()
    {
        var names = Glyphs.Names.ToList();

        Assert.True(names.Count >= 20);

        foreach (var name in names)
        {
            Assert.True(Glyphs.TryGet(name, out var lines));
            Assert.All(lines.SelectMany(l => l.Points), p =>
            {
                Assert.InRange(p.X, 0, Glyphs.Grid);
                Assert.InRange(p.Y, 0, Glyphs.Grid);
            });
        }
    }
}
