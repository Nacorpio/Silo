using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ColorSurfaceTests
{
    static ColorSurface Create(ColorSurfaceKind kind)
    {
        var surface = new ColorSurface(kind);
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "ColorSurface": { "width": "100px", "height": "50px" } } }""", "t.jsonc")]);

        root.Children.Add(surface);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(200, 100));

        return surface;
    }

    [Fact]
    public void Dragging_moves_the_thumb_and_clamps_it_to_the_surface()
    {
        var square = Create(ColorSurfaceKind.SaturationValue);
        var raised = 0;

        square.Changed += (_, _) => raised++;

        Assert.True(square.PointerDown(50, 25));
        Assert.Equal((0.5, 0.5), (square.X, square.Y));

        Assert.True(square.PointerDrag(500, -20));
        Assert.Equal((1.0, 0.0), (square.X, square.Y));

        square.PointerUp();

        Assert.False(square.PointerDrag(10, 10));
        Assert.Equal(2, raised);
    }

    [Fact]
    public void A_bar_ignores_the_vertical_position()
    {
        var bar = Create(ColorSurfaceKind.Hue);

        bar.PointerDown(25, 40);

        Assert.Equal((0.25, 0.0), (bar.X, bar.Y));
    }

    [Fact]
    public void Setting_the_position_from_code_does_not_raise_changed()
    {
        var bar = Create(ColorSurfaceKind.Alpha);
        var raised = 0;

        bar.Changed += (_, _) => raised++;

        bar.X = 0.7;

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Home_and_end_go_to_the_ends_of_a_bar_but_do_nothing_on_the_square()
    {
        var bar = Create(ColorSurfaceKind.Hue);
        var square = Create(ColorSurfaceKind.SaturationValue);

        bar.KeyDown(Key.End);
        square.KeyDown(Key.End);

        Assert.Equal(1, bar.X);
        Assert.Equal(0, square.X);
    }

    [Fact]
    public void Painting_draws_a_gradient_for_every_kind()
    {
        foreach (var kind in Enum.GetValues<ColorSurfaceKind>())
        {
            var surface = Create(kind);
            var builder = new Silo.Rendering.DisplayListBuilder();

            surface.Render(builder);

            Assert.Contains(builder.Build().Commands, c => c is Silo.Rendering.FillGradientCommand);
        }
    }
}
