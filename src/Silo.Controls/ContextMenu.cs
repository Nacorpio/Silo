using Silo.Layout;

namespace Silo;

/// <summary>
/// A menu of entries that opens at a point, above the window content.
/// </summary>
/// <remarks>
/// <para>
/// Give a Control a menu with <see cref="Control.ContextMenu"/> and it opens at the pointer when the secondary button is
/// pressed on that Control. Open one yourself with <see cref="Show"/>. The row under the pointer is highlighted, a press on
/// it runs the entry's <see cref="MenuItem.Click"/> after the menu closed, and a press anywhere else or Escape closes the
/// menu. The menu is moved as far as needed to lie inside the window.
/// </para>
/// <para>
/// A ContextMenu is a ListView, so it is styled with <c>selection-color</c>, <c>selection-text-color</c> and
/// <c>item-height</c> (28px by default) next to the usual box properties. It is as wide as its longest entry.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var menu = new ContextMenu
/// {
///     Entries = [new MenuItem("Copy", Copy), new MenuItem("Paste", Paste)],
/// };
///
/// editor.ContextMenu = menu;
/// </code>
/// </example>
public class ContextMenu : ListView
{
    /// <summary>The entries.</summary>
    IReadOnlyList<MenuItem> entries = [];


    /// <summary>Creates an empty menu.</summary>
    public ContextMenu()
    {
        HoverSelects = true;

        Activated += (_, index) => Pick(index);
    }


    /// <summary>Gets or sets the entries.</summary>
    public IReadOnlyList<MenuItem> Entries
    {
        get => entries;
        set
        {
            entries = value ?? [];

            Items = entries.Select(entry => entry.Text).ToList();
        }
    }

    /// <summary>Gets whether the menu is shown.</summary>
    public bool IsOpen => Overlays?.Contains(this) == true;

    /// <summary>The menu never takes the keyboard focus, which stays where it was.</summary>
    public override bool IsFocusable => false;


    /// <summary>Opens the menu.</summary>
    /// <param name="anchor">A Control in the window, which tells the menu which window to open in.</param>
    /// <param name="x">The horizontal position of the menu's left edge, relative to the window.</param>
    /// <param name="y">The vertical position of the menu's top edge, relative to the window.</param>
    /// <returns><see langword="true"/> when it opened. It does not when it has no entries, is open, or the anchor is not in a window.</returns>
    public bool Show(Control anchor, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (entries.Count == 0 || IsOpen || anchor.Overlays is not { } layer)
            return false;

        SelectedIndex = -1;
        Overlays = layer;

        layer.Show(this, x, y);

        return true;
    }

    /// <summary>Closes the menu without running an entry.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close() => Overlays?.Close(this) == true;

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var width = entries.Select(entry => measurer.Measure(entry.Text, style).Width).DefaultIfEmpty(0).Max();

        return new Size(width + 2 * TextInset, entries.Count * Math.Max(1, GetValue(ItemHeightProperty)));
    }

    /// <summary>Closes the menu and runs an entry.</summary>
    /// <param name="index">The entry's index.</param>
    void Pick(int index)
    {
        Close();

        entries[index].Click?.Invoke();
    }
}
