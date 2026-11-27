using Silo.Layout;
using Silo.Rendering;
using Xunit;

public class TextLayoutTests
{
    static readonly TextStyle Style = new("Inter", 14);

    /// <summary>Counts how often it is asked, and gives every character 7 pixels.</summary>
    sealed class CountingMeasurer : ITextMeasurer
    {
        public int Calls { get; private set; }

        public Size Measure(string text, TextStyle style)
        {
            Calls++;

            return new Size(text.Length * 7, 14);
        }
    }

    [Fact]
    public void The_width_to_an_index_is_the_width_of_that_many_characters()
    {
        var layout = new TextLayout(new CountingMeasurer(), "hello", Style);

        Assert.Equal(0, layout.WidthTo(0));
        Assert.Equal(14, layout.WidthTo(2));
        Assert.Equal(35, layout.Width);
        Assert.Equal(14, layout.LineHeight);
    }

    [Fact]
    public void Indexes_outside_the_text_are_clamped()
    {
        var layout = new TextLayout(new CountingMeasurer(), "ab", Style);

        Assert.Equal(0, layout.WidthTo(-3));
        Assert.Equal(14, layout.WidthTo(9));
    }

    [Fact]
    public void A_position_gives_the_nearest_boundary_and_the_first_one_on_a_tie()
    {
        var layout = new TextLayout(new CountingMeasurer(), "abcd", Style);

        Assert.Equal(0, layout.IndexAt(-50));
        Assert.Equal(1, layout.IndexAt(6));
        Assert.Equal(0, layout.IndexAt(3.5));
        Assert.Equal(2, layout.IndexAt(3.6 + 7));
        Assert.Equal(4, layout.IndexAt(500));
    }

    [Fact]
    public void Empty_text_has_one_boundary_and_asks_the_measurer_nothing()
    {
        var measurer = new CountingMeasurer();
        var layout = new TextLayout(measurer, string.Empty, Style);

        Assert.Equal(0, layout.Width);
        Assert.Equal(0, layout.IndexAt(20));
        Assert.Equal(0, measurer.Calls);
    }

    [Fact]
    public void The_text_is_measured_once_however_often_the_layout_is_asked()
    {
        var measurer = new CountingMeasurer();
        var layout = new TextLayout(measurer, "abcdef", Style);

        layout.WidthTo(1);
        layout.WidthTo(5);
        layout.IndexAt(20);
        layout.IndexAt(30);

        Assert.Equal(6, measurer.Calls);
    }

    [Fact]
    public void A_layout_fits_only_the_measurer_text_and_font_it_was_made_for()
    {
        var measurer = new CountingMeasurer();
        var layout = new TextLayout(measurer, "ab", Style);

        Assert.True(layout.IsFor(measurer, "ab", Style));
        Assert.False(layout.IsFor(measurer, "abc", Style));
        Assert.False(layout.IsFor(measurer, "ab", Style with { Size = 16 }));
        Assert.False(layout.IsFor(new CountingMeasurer(), "ab", Style));
    }
}
