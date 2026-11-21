namespace Silo;

/// <summary>
/// Shows a single line of text.
/// </summary>
/// <remarks>
/// The text color, font and size come from the <c>color</c>, <c>font-family</c>, <c>font-size</c> and <c>font-weight</c>
/// properties, which are inherited, so setting them on a parent Box styles all text inside it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var title = new TextBlock { Text = "Settings" };
///
/// title.AddClass("heading");
/// </code>
/// </example>
public class TextBlock : TextControl
{
}
