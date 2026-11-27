namespace Silo;

/// <summary>
/// The font families that emoji are drawn in.
/// </summary>
/// <remarks>
/// Skia draws a string with a single typeface and does not fall back to another font for a glyph that is missing, so a Control that
/// shows emoji names an emoji font itself.
/// </remarks>
public static class EmojiFonts
{
    /// <summary>The emoji fonts of Windows, macOS and Linux, in that order. The first one that is installed is used.</summary>
    public const string Default = "Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji";
}
