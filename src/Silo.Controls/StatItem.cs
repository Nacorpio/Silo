using Silo.Rendering;

namespace Silo;

/// <summary>One row of a <see cref="StatList"/>: an icon, a bold value and a muted label, such as <c>star</c>, "1" and "star".</summary>
/// <param name="Glyph">The name of a glyph from the built-in set, or empty for none.</param>
/// <param name="Value">The bold part, such as "1.2k". A string so that it can hold any format. Empty for a row of only text.</param>
/// <param name="Label">The muted text after the value.</param>
/// <param name="Image">An image shown instead of the glyph, scaled to <c>icon-size</c>, or <see langword="null"/>.</param>
/// <example>
/// <code language="csharp">
/// stats.Items = [new("activity", Label: "Activity"), new("star", "1", "star"), new("eye", "0", "watching")];
/// </code>
/// </example>
public sealed record StatItem(string Glyph = "", string Value = "", string Label = "", IImage? Image = null);
