using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Styling;
using Silo.Styling.Values;
using SkiaSharp;
using Xunit;

public class ButtonTests
{
    const string Sheet = """
        {
          "tokens": { "--accent": "#3b82f6" },
          "rules": {
            "Box": { "align-items": "start" },
            "Button": {
              "background": "#cccccc", "padding": "6px 12px", "border-radius": "4px", "text-align": "center",
              "&:hover": { "background": "var(--accent)" },
              "&:active": { "background": "#1d4ed8" }
            }
          }
        }
        """;

    static StyleResolver Resolver() => new([JsoncStylesheetReader.Read(Sheet, "theme.jsonc")]);

    static (Box Root, Button Button) Setup()
    {
        var button = new Button { Text = "Save" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(button);

        return (root, button);
    }

    static Color BackgroundOf(Control root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands.OfType<FillRectCommand>().Last().Color;
    }

    [Fact]
    public void Hover_and_active_pseudo_states_restyle_the_button()
    {
        var (root, button) = Setup();
        var resolver = Resolver();

        root.ApplyStyles(resolver);
        root.Arrange(new Size(300, 100));

        Assert.Equal(new Color(204, 204, 204), BackgroundOf(root));

        button.SetHovered(true);
        root.ApplyStyles(resolver);

        Assert.Equal(new Color(59, 130, 246), BackgroundOf(root));

        button.PointerDown();
        root.ApplyStyles(resolver);

        Assert.Equal(new Color(29, 78, 216), BackgroundOf(root));
    }

    [Fact]
    public void Releasing_over_the_button_raises_click_once()
    {
        var (_, button) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        button.PointerDown();
        button.PointerUp(inside: true);

        Assert.Equal(1, clicks);
        Assert.DoesNotContain("active", button.PseudoStates);
    }

    [Fact]
    public void Releasing_outside_or_without_a_press_does_not_click()
    {
        var (_, button) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        button.PointerDown();
        button.PointerUp(inside: false);
        button.PointerUp(inside: true);

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void Button_size_is_its_text_plus_padding()
    {
        var (root, button) = Setup();

        root.ApplyStyles(Resolver());
        root.Arrange(new Size(300, 100));

        Assert.Equal(28 + 24, button.Bounds.Width);
        Assert.Equal(14 + 12, button.Bounds.Height);
    }

    [Fact]
    public void A_styled_button_renders_to_real_pixels_with_skia()
    {
        var button = new Button { Text = "Save" };
        var root = new Box { TextMeasurer = new SkiaTextMeasurer() };

        root.Name = "root";
        root.Children.Add(button);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""
            { "rules": { "#root": { "align-items": "start" }, "Button": { "background": "#3b82f6", "padding": "10px 20px", "color": "#ffffff" } } }
            """, "t.jsonc")]));
        root.Arrange(new Size(200, 80));

        var builder = new DisplayListBuilder();

        root.Render(builder);

        using var bitmap = SkiaRenderer.Render(builder.Build(), 200, 80, new Color(255, 255, 255));

        Assert.Equal(new SKColor(59, 130, 246), bitmap.GetPixel(2, 2));
        Assert.Equal(new SKColor(255, 255, 255), bitmap.GetPixel(190, 70));
    }
}
