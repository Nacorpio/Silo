using Silo;
using Silo.Styling.Values;
using Xunit;

public class ColorPanelTests
{
    [Theory]
    [InlineData(255, 0, 0, 0.0, 1.0, 1.0)]
    [InlineData(0, 255, 0, 120.0, 1.0, 1.0)]
    [InlineData(0, 0, 255, 240.0, 1.0, 1.0)]
    [InlineData(255, 255, 255, 0.0, 0.0, 1.0)]
    [InlineData(0, 0, 0, 0.0, 0.0, 0.0)]
    public void Hsv_conversion_goes_both_ways_for_the_corner_colors(int r, int g, int b, double h, double s, double v)
    {
        var color = new Color((byte)r, (byte)g, (byte)b);

        Assert.Equal((h, s, v), ColorPanel.ToHsv(color));
        Assert.Equal(color, ColorPanel.FromHsv(h, s, v));
    }

    [Fact]
    public void Hsv_conversion_round_trips_an_ordinary_color()
    {
        var color = new Color(59, 130, 246);
        var (h, s, v) = ColorPanel.ToHsv(color);

        Assert.Equal(color, ColorPanel.FromHsv(h, s, v));
    }

    [Fact]
    public void Setting_the_color_shows_it_in_the_hex_field_and_raises_the_event()
    {
        var panel = new ColorPanel();
        var raised = 0;

        panel.ColorChanged += (_, _) => raised++;

        panel.Color = new Color(255, 0, 128);
        panel.Color = new Color(255, 0, 128);

        Assert.Equal(1, raised);
        Assert.Equal("#ff0080", Hex(panel).Text);

        panel.Color = new Color(255, 0, 128, 64);

        Assert.Equal("#ff008040", Hex(panel).Text);
    }

    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("00ff00", 0, 255, 0, 255)]
    [InlineData("#0000ff80", 0, 0, 255, 128)]
    public void Typing_a_whole_hex_color_applies_it_while_the_text_stays_as_typed(string text, int r, int g, int b, int a)
    {
        var panel = new ColorPanel();

        Hex(panel).Text = text;

        Assert.Equal(new Color((byte)r, (byte)g, (byte)b, (byte)a), panel.Color);
        Assert.Equal(text, Hex(panel).Text);
    }

    [Fact]
    public void Half_a_color_is_ignored_and_enter_goes_back_to_the_current_color()
    {
        var panel = new ColorPanel { Color = new Color(1, 2, 3) };
        var submitted = 0;

        panel.Submitted += (_, _) => submitted++;

        Hex(panel).Text = "#12";

        Assert.Equal(new Color(1, 2, 3), panel.Color);

        Hex(panel).KeyDown(Key.Enter);

        Assert.Equal("#010203", Hex(panel).Text);
        Assert.Equal(1, submitted);
    }

    [Fact]
    public void A_gray_keeps_the_hue_the_panel_had()
    {
        var panel = new ColorPanel { Color = new Color(0, 0, 255) };
        var square = Surface(panel, 0);

        panel.Color = new Color(128, 128, 128);

        Assert.Equal(240, square.Hue);
    }

    [Fact]
    public void The_alpha_bar_is_only_there_with_show_alpha()
    {
        var panel = new ColorPanel();

        Assert.Equal(3, panel.Children.Count);

        panel.ShowAlpha = true;

        Assert.Equal(4, panel.Children.Count);
        Assert.Equal(ColorSurfaceKind.Alpha, ((ColorSurface)panel.Children[2]).Kind);

        panel.ShowAlpha = false;

        Assert.Equal(3, panel.Children.Count);
    }

    [Fact]
    public void The_keys_move_the_thumbs_and_tab_moves_between_the_parts()
    {
        var panel = new ColorPanel { Color = new Color(255, 0, 0) };

        panel.BeginKeyboard();

        Assert.True(panel.KeyDown(Key.Left, KeyModifiers.Shift));
        Assert.Equal(new Color(255, 25, 25), panel.Color);

        panel.KeyDown(Key.Tab, KeyModifiers.None);
        panel.KeyDown(Key.Right, KeyModifiers.Shift);

        Assert.Equal(ColorPanel.FromHsv(36, 0.9, 1), panel.Color);

        Assert.False(panel.TextInput("a"));

        panel.KeyDown(Key.Tab, KeyModifiers.None);

        Assert.True(panel.TextInput("a"));
    }

    [Fact]
    public void Enter_on_a_surface_submits()
    {
        var panel = new ColorPanel();
        var submitted = 0;

        panel.Submitted += (_, _) => submitted++;
        panel.BeginKeyboard();

        panel.KeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(1, submitted);
    }

    static TextBox Hex(ColorPanel panel) => (TextBox)panel.Children[^1];

    static ColorSurface Surface(ColorPanel panel, int index) => (ColorSurface)panel.Children[index];
}
