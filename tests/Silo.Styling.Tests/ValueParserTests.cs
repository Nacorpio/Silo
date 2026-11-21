using Silo.Styling.Values;
using Xunit;

public class ValueParserTests
{
    [Theory]
    [InlineData("12", 12, LengthUnit.Px)]
    [InlineData("12px", 12, LengthUnit.Px)]
    [InlineData("50%", 50, LengthUnit.Percent)]
    [InlineData("1.5em", 1.5, LengthUnit.Em)]
    [InlineData("2rem", 2, LengthUnit.Rem)]
    [InlineData("1fr", 1, LengthUnit.Fr)]
    public void Parses_lengths(string text, double value, LengthUnit unit)
    {
        Assert.True(Length.TryParse(text, out var length));
        Assert.Equal(new Length(value, unit), length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("px")]
    [InlineData("12pt")]
    [InlineData("calc(1px)")]
    public void Rejects_bad_lengths(string text) => Assert.False(Length.TryParse(text, out _));

    [Fact]
    public void Expands_thickness_shorthand_like_css()
    {
        Assert.True(Thickness.TryParse("8 16", out var two));
        Assert.Equal(new Length(8, LengthUnit.Px), two.Top);
        Assert.Equal(new Length(16, LengthUnit.Px), two.Left);
        Assert.True(Thickness.TryParse("1 2 3", out var three));
        Assert.Equal(new Length(2, LengthUnit.Px), three.Left);
        Assert.False(Thickness.TryParse("1 2 3 4 5", out _));
    }

    [Theory]
    [InlineData("#3b82f6", 0x3b, 0x82, 0xf6, 255)]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("#00000080", 0, 0, 0, 0x80)]
    [InlineData("rgb(10, 20, 30)", 10, 20, 30, 255)]
    [InlineData("rgb(10 20 30 / 50%)", 10, 20, 30, 128)]
    [InlineData("hsl(0 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(120, 100%, 25%)", 0, 128, 0, 255)]
    [InlineData("red", 255, 0, 0, 255)]
    [InlineData("transparent", 0, 0, 0, 0)]
    public void Parses_colors(string text, int r, int g, int b, int a)
    {
        Assert.True(Color.TryParse(text, out var color));
        Assert.Equal(new Color((byte)r, (byte)g, (byte)b, (byte)a), color);
    }

    [Fact]
    public void Parses_oklch_close_to_the_known_srgb_value()
    {
        Assert.True(Color.TryParse("oklch(0.628 0.2577 29.23)", out var color));
        Assert.InRange(color.R, 250, 255);
        Assert.InRange(color.G, 0, 8);
        Assert.InRange(color.B, 0, 8);
    }

    [Theory]
    [InlineData("#12")]
    [InlineData("rgb(1, 2)")]
    [InlineData("nope")]
    [InlineData("lab(1 2 3)")]
    public void Rejects_bad_colors(string text) => Assert.False(Color.TryParse(text, out _));
}
