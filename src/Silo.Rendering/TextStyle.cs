namespace Silo.Rendering;

/// <summary>
/// Describes the font that text is measured and drawn with.
/// </summary>
/// <param name="FontFamily">The font family name, such as <c>"Inter"</c>. An unknown family falls back to the platform default.</param>
/// <param name="Size">The font size in device-independent pixels.</param>
/// <param name="Bold">Whether to use a bold weight.</param>
/// <param name="Italic">Whether to use an italic slant.</param>
/// <example>
/// <code language="csharp">
/// var heading = new TextStyle("Inter", 20, Bold: true);
/// </code>
/// </example>
public readonly record struct TextStyle(string FontFamily, double Size, bool Bold = false, bool Italic = false);
