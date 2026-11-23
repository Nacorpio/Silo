using Silo.Rendering;

namespace Silo;

/// <summary>One entry of a <see cref="ContextMenu"/> or of a menu of a <see cref="MenuBar"/>.</summary>
/// <param name="Text">
/// The text of the entry. An ampersand marks the next letter as the access key, which is underlined and picks the entry when
/// it is pressed while the menu is open, so <c>&amp;Copy</c> shows <c>Copy</c> with the C underlined. Use <c>&amp;&amp;</c> for a
/// literal ampersand.
/// </param>
/// <param name="Click">What to do when the entry is picked, after the menu closed, or <see langword="null"/> for nothing.</param>
/// <param name="Icon">A small picture shown in front of the text, or <see langword="null"/> for none.</param>
/// <remarks>
/// <para>
/// Give an entry <see cref="Children"/> to make it open a submenu instead of running <paramref name="Click"/>. Use
/// <see cref="Separator"/> for a line between groups of entries.
/// </para>
/// <para>
/// Set <see cref="IsChecked"/> to make an entry a check item, which shows a check mark while it is on and flips when it is picked.
/// Give several entries of one list the same <see cref="GroupName"/> to make them radio items: picking one turns it on and the
/// others of its group off. <see cref="IsEnabled"/> turns an entry off so that it is dimmed and cannot be picked, and
/// <see cref="ShortcutText"/> shows a key combination at the right edge. The menu only displays the shortcut; it does not handle the key.
/// <see cref="Glyph"/> picks a built-in vector glyph as the icon, instead of an image.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var copy = new MenuItem("&amp;Copy", () => clipboard.Set(selection), copyIcon);
/// var recent = new MenuItem("Open &amp;Recent") { Children = [new MenuItem("a.txt"), new MenuItem("b.txt")] };
///
/// ContextMenu menu = new() { Entries = [copy, MenuItem.Separator, recent] };
/// </code>
/// </example>
public sealed record MenuItem(string Text, Action? Click = null, IImage? Icon = null)
{
    /// <summary>Gets a line between two groups of entries. It cannot be selected or picked.</summary>
    public static MenuItem Separator => new(string.Empty) { IsSeparator = true };

    /// <summary>Gets whether the entry is a line between groups of entries.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>
    /// Gets or sets whether the entry is checked, or <see langword="null"/> when it is not a check or radio item, which is the default.
    /// The menu changes it when the entry is picked.
    /// </summary>
    public bool? IsChecked { get; set; }

    /// <summary>Gets the name of the radio group the entry belongs to, or <see langword="null"/> for a plain check item. It has no effect without <see cref="IsChecked"/>.</summary>
    public string? GroupName { get; init; }

    /// <summary>Gets whether the entry can be picked. The default is <see langword="true"/>. An entry that is off is dimmed and skipped by the keyboard.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the name of a built-in vector glyph shown in front of the text, such as <c>check</c> or <c>search</c>, or <see langword="null"/>
    /// for none. See <see cref="Glyphs"/>. It is tinted by the text color. When the entry also has an <c>Icon</c>, the icon is drawn.
    /// </summary>
    public string? Glyph { get; init; }

    /// <summary>Gets the text of the key combination shown at the right edge, such as <c>Ctrl+C</c>, or <see langword="null"/> for none.</summary>
    public string? ShortcutText { get; init; }

    /// <summary>Gets the entries of the submenu that the entry opens, or <see langword="null"/> for an entry that runs <see cref="Click"/>.</summary>
    public IReadOnlyList<MenuItem>? Children { get; init; }
}
