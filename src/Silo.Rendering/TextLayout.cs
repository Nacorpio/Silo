namespace Silo.Rendering;

/// <summary>
/// One line of text that has been measured once, so that caret positions, selections and hit tests are cheap lookups.
/// </summary>
/// <remarks>
/// <para>
/// The prefix widths are computed the first time they are needed and kept. A layout is immutable: when the text or the font changes,
/// make a new one, or ask <see cref="IsFor"/> whether the one you hold still fits.
/// </para>
/// <para>
/// Offsets are UTF-16 indexes into the text, as in <see cref="string"/>. Out of range values are clamped.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var layout = new TextLayout(measurer, "hello", style);
///
/// double caretX = layout.WidthTo(2);
/// int offset = layout.IndexAt(pointerX);
/// </code>
/// </example>
public sealed class TextLayout
{
    readonly ITextMeasurer measurer;
    readonly TextStyle style;
    double[]? widths;

    /// <summary>Creates a layout of a line of text.</summary>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="text">The text, without line breaks.</param>
    /// <param name="style">The font.</param>
    public TextLayout(ITextMeasurer measurer, string text, TextStyle style)
    {
        this.measurer = measurer;
        this.style = style;

        Text = text;
    }

    /// <summary>Gets the text that was measured.</summary>
    public string Text { get; }

    /// <summary>Gets the height of a line in this font.</summary>
    public double LineHeight => measurer.LineHeight(style);

    /// <summary>Gets the width of the whole text.</summary>
    public double Width => Widths[^1];

    double[] Widths => widths ??= Text.Length == 0 ? [0] : measurer.PrefixWidths(Text, style);

    /// <summary>Tells whether this layout was made for the given measurer, text and font.</summary>
    /// <param name="measurer">The measurer.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The font.</param>
    /// <returns><see langword="true"/> when the layout can be reused.</returns>
    public bool IsFor(ITextMeasurer measurer, string text, TextStyle style)
        => ReferenceEquals(this.measurer, measurer) && this.style == style && Text == text;

    /// <summary>Gets the width of the first characters of the text, which is where a caret at that offset is drawn.</summary>
    /// <param name="index">The number of characters, clamped to the text.</param>
    /// <returns>The width in pixels.</returns>
    public double WidthTo(int index) => Widths[Math.Clamp(index, 0, Text.Length)];

    /// <summary>Gets the character boundary nearest to a horizontal position.</summary>
    /// <param name="x">The position, relative to the start of the text. Before the start gives 0, after the end gives the text length.</param>
    /// <returns>The offset into the text. When two boundaries are equally near, the first wins.</returns>
    public int IndexAt(double x)
    {
        var all = Widths;
        var best = 0;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < all.Length; i++)
        {
            var distance = Math.Abs(all[i] - x);

            if (distance >= bestDistance)
                continue;

            best = i;
            bestDistance = distance;
        }

        return best;
    }
}
