namespace Silo;

/// <summary>An entry of a <see cref="NavigationView"/>: a destination with an optional icon, or a header above a group.</summary>
/// <param name="Text">The text of the entry.</param>
/// <param name="Glyph">The name of a glyph from <see cref="Glyphs"/> shown before the text. Empty shows none.</param>
/// <param name="IsHeader"><see langword="true"/> for a title above a group, which cannot be selected.</param>
/// <example>
/// <code language="csharp">
/// nav.Items =
/// [
///     new NavigationItem("Home", "home"),
///     new NavigationItem("Library", IsHeader: true),
///     new NavigationItem("Search", "search"),
/// ];
/// </code>
/// </example>
public sealed record NavigationItem(string Text, string Glyph = "", bool IsHeader = false);
