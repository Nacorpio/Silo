using Silo;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class DefaultThemeTests
{
    static StyleResolver Resolver() => new([DefaultTheme.Load()]);

    [Fact]
    public void The_embedded_theme_parses_without_diagnostics()
    {
        var button = new Button { Text = "Ok" };

        var diagnostics = button.ApplyStyles(Resolver());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Buttons_use_the_accent_and_change_on_hover_and_press()
    {
        var button = new Button { Text = "Ok" };

        button.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), button.GetValue(Control.BackgroundProperty));

        button.SetHovered(true);
        button.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x25, 0x63, 0xeb), button.GetValue(Control.BackgroundProperty));

        button.SetPressed(true);
        button.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x1d, 0x4e, 0xd8), button.GetValue(Control.BackgroundProperty));
    }

    [Fact]
    public void Text_inherits_the_theme_color_and_font_size()
    {
        var root = new Box();
        var text = new TextBlock { Text = "Hi" };

        root.Children.Add(text);
        root.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x1f, 0x29, 0x37), text.GetValue(Control.ColorProperty));
        Assert.Equal(14, text.GetValue(Control.FontSizeProperty));
    }
}
