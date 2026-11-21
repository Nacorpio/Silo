using SkiaSharp;
using Silo.Layout;

namespace Silo.Rendering.Skia;

/// <summary>
/// Measures text with Skia's font metrics, the same ones that <see cref="SkiaDrawingContext"/> draws with.
/// </summary>
/// <remarks>
/// Only single lines are measured. Wrapping and shaping arrive with Silo.Text. The line height is the font's own spacing
/// (ascent plus descent plus leading).
/// </remarks>
/// <example>
/// <code language="csharp">
/// var measurer = new SkiaTextMeasurer();
///
/// Size size = measurer.Measure("Save", new TextStyle("Inter", 14));
/// </code>
/// </example>
public sealed class SkiaTextMeasurer : ITextMeasurer
{
    /// <inheritdoc/>
    public Size Measure(string text, TextStyle style)
    {
        using var typeface = CreateTypeface(style);
        using var font = new SKFont(typeface, (float)style.Size);

        return new Size(font.MeasureText(text), font.Spacing);
    }

    /// <summary>Resolves a text style to a Skia typeface, falling back to the platform default.</summary>
    /// <param name="style">The text style.</param>
    /// <returns>A typeface that the caller disposes.</returns>
    internal static SKTypeface CreateTypeface(TextStyle style)
    {
        var weight = style.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = style.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        return SKTypeface.FromFamilyName(style.FontFamily, weight, SKFontStyleWidth.Normal, slant)
            ?? SKTypeface.Default;
    }
}
