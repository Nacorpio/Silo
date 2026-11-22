using SkiaSharp;
using Silo.Layout;

namespace Silo.Rendering.Skia;

/// <summary>
/// Measures text with Skia's font metrics, the same ones that <see cref="SkiaDrawingContext"/> draws with.
/// </summary>
/// <remarks>
/// <para>
/// A font family may be a comma-separated list, as in CSS: <c>"Fira Code, Consolas, monospace"</c> uses the first family that
/// is installed, and the platform default when none is.
/// </para>
/// <para>
/// Only single lines are measured. Wrapping and shaping arrive with Silo.Text. The line height is the font's own spacing
/// (ascent plus descent plus leading).
/// </para>
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
    /// <summary>The family names that the platform resolves itself, so that whatever it returns for them is the answer.</summary>
    static readonly HashSet<string> GenericFamilies =
        new(["serif", "sans-serif", "monospace", "system-ui", "ui-monospace"], StringComparer.OrdinalIgnoreCase);


    /// <inheritdoc/>
    public Size Measure(string text, TextStyle style)
    {
        using var typeface = CreateTypeface(style);
        using var font = new SKFont(typeface, (float)style.Size);

        return new Size(font.MeasureText(text), font.Spacing);
    }

    /// <summary>Resolves a text style to a Skia typeface, falling back to the platform default.</summary>
    /// <param name="style">The text style. Its family may be a list such as <c>"Fira Code, Consolas, monospace"</c>: the first family that is installed wins.</param>
    /// <returns>A typeface that the caller disposes.</returns>
    internal static SKTypeface CreateTypeface(TextStyle style)
    {
        var weight = style.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = style.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        SKTypeface? fallback = null;

        foreach (var name in style.FontFamily.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var family = name.Trim('"', '\'', ' ');
            var typeface = SKTypeface.FromFamilyName(family, weight, SKFontStyleWidth.Normal, slant);

            if (typeface is not null && (GenericFamilies.Contains(family) || IsFamily(typeface, family)))
            {
                fallback?.Dispose();

                return typeface;
            }

            fallback ??= typeface;

            if (!ReferenceEquals(fallback, typeface))
                typeface?.Dispose();
        }

        return fallback ?? SKTypeface.Default;
    }

    /// <summary>Tells whether a typeface really is a family, and not the platform's stand-in for a family that is not installed.</summary>
    /// <param name="typeface">The typeface that Skia returned.</param>
    /// <param name="family">The family that was asked for.</param>
    /// <returns><see langword="true"/> when the names agree, ignoring case.</returns>
    static bool IsFamily(SKTypeface typeface, string family)
        => string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase);
}
