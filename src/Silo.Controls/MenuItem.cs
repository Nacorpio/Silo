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

    /// <summary>Gets the entries of the submenu that the entry opens, or <see langword="null"/> for an entry that runs <see cref="Click"/>.</summary>
    public IReadOnlyList<MenuItem>? Children { get; init; }
}
