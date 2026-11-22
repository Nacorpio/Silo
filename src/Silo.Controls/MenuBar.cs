using Silo.Layout;

namespace Silo;

/// <summary>A row of menu titles, each of which opens a menu of entries below it.</summary>
/// <remarks>
/// <para>
/// Clicking a title opens its menu below it, and clicking the same title again closes it. While a menu is open, moving the
/// pointer over another title switches to that title's menu, and a press outside the menus closes it. A menu is a
/// <see cref="ContextMenu"/>, so it runs an entry and closes when the entry is pressed, and Escape closes it.
/// </para>
/// <para>
/// Style the titles with the <c>MenuBarItem</c> type and an open title with <c>MenuBarItem:open</c>. The Default theme lays
/// the bar out in a row.
/// </para>
/// <para>The bar needs a window to open its menus in, so it must be in the tree of a window.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var bar = new MenuBar();
///
/// bar.AddMenu("File", [new MenuItem("Open", Open), new MenuItem("Exit", Exit)]);
/// bar.AddMenu("Help", [new MenuItem("About", ShowAbout)]);
/// </code>
/// </example>
public class MenuBar : Control
{
    /// <summary>The title whose menu is shown.</summary>
    MenuBarItem? current;

    /// <summary>Gets the title whose menu is shown, or <see langword="null"/> when no menu is shown.</summary>
    public MenuBarItem? OpenItem => current;

    /// <summary>Gets whether a menu is shown.</summary>
    public bool IsOpen => current is not null;

    /// <summary>Adds a title with a menu after the existing ones.</summary>
    /// <param name="title">The text of the title.</param>
    /// <param name="entries">The entries of the menu.</param>
    /// <returns>The title, so that code can add a Class to it.</returns>
    public MenuBarItem AddMenu(string title, IReadOnlyList<MenuItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var item = new MenuBarItem(new ContextMenu { Entries = entries }) { Text = title };

        Children.Add(item);

        return item;
    }

    /// <summary>Opens the menu of a title, closing the menu that was shown before.</summary>
    /// <param name="item">A title of this bar.</param>
    /// <returns><see langword="true"/> when the menu opened. It does not when it is open already, has no entries or the bar is not in a window.</returns>
    public bool Open(MenuBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (current == item || item.Parent != this || Overlays is not { } layer)
            return false;

        Close();

        var (x, y) = item.ToLocal(0, 0);

        item.IsOpen = true;
        layer.Changed += OnOverlaysChanged;

        if (item.Menu.Show(this, -x, -y + item.Bounds.Height))
        {
            current = item;

            return true;
        }

        item.IsOpen = false;
        layer.Changed -= OnOverlaysChanged;

        return false;
    }

    /// <summary>Closes the menu that is shown.</summary>
    /// <returns><see langword="true"/> when a menu was shown.</returns>
    public bool Close()
    {
        if (current is not { } item)
            return false;

        current = null;
        item.IsOpen = false;

        if (Overlays is { } layer)
            layer.Changed -= OnOverlaysChanged;

        item.Menu.Close();

        return true;
    }

    /// <summary>Opens the menu of a title, or closes it when it is the one that is shown.</summary>
    /// <param name="item">A title of this bar.</param>
    /// <returns><see langword="true"/> when a menu opened or closed.</returns>
    public bool Toggle(MenuBarItem item) => current == item ? Close() : Open(item);

    /// <summary>Forgets the open title when its menu was closed by something else, such as Escape or a press on an entry.</summary>
    /// <param name="sender">The overlay layer.</param>
    /// <param name="e">Nothing.</param>
    void OnOverlaysChanged(object? sender, EventArgs e)
    {
        if (current is { Menu.IsOpen: false })
            Close();
    }
}
