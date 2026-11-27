using Silo.Layout;

namespace Silo.Rendering;

/// <summary>
/// Measures text, so that layout can size a TextBlock before anything is drawn.
/// </summary>
/// <remarks>
/// The measurer and the drawing context of a backend must agree: the size returned here is the line box that
/// <see cref="IDrawingContext.DrawText"/> draws into.
/// </remarks>
public interface ITextMeasurer
{
    /// <summary>Measures a single line of text.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="style">The font to measure with.</param>
    /// <returns>The size of the line box: the advance width of the text and the line height of the font.</returns>
    Size Measure(string text, TextStyle style);

    /// <summary>Gets the line height of a font, which is the same whatever the text.</summary>
    /// <param name="style">The font.</param>
    /// <returns>The height of a line box.</returns>
    /// <remarks>The default measures <c>"M"</c>. A backend overrides it when it can read the font's metrics directly.</remarks>
    double LineHeight(TextStyle style) => Measure("M", style).Height;

    /// <summary>Gets the width of every prefix of a single line of text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The font.</param>
    /// <returns>An array of <c>text.Length + 1</c> widths: element <c>i</c> is the width of the first <c>i</c> characters, so element 0 is 0.</returns>
    /// <remarks>
    /// The default measures each prefix, which is correct for any measurer but slow. A backend overrides it with one pass over the text.
    /// Use <see cref="TextLayout"/> instead of calling this directly.
    /// </remarks>
    double[] PrefixWidths(string text, TextStyle style)
    {
        var widths = new double[text.Length + 1];

        for (var i = 1; i <= text.Length; i++)
            widths[i] = Measure(text[..i], style).Width;

        return widths;
    }
}
