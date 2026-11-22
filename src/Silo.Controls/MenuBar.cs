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
/// <para>
/// Alt with the access key of a title opens its menu: write <c>&amp;File</c> to make F the key. A window passes key presses to
/// <see cref="AccessKeyDown"/> for that. While a menu is open, Left and Right move to the menu of the title next to it, and the
/// menu takes the other keys, as described on <see cref="ContextMenu"/>.
/// </para>
/// <para>The bar needs a window to open its menus in, so it must be in the tree of a window.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var bar = new MenuBar();
///
/// bar.AddMenu("&amp;File", [new MenuItem("&amp;Open", Open), MenuItem.Separator, new MenuItem("E&amp;xit", Exit)]);
/// bar.AddMenu("&amp;Help", [new MenuItem("&amp;About", ShowAbout)]);
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
    /// <param name="title">The text of the title. An ampersand marks the next letter as the access key.</param>
    /// <param name="entries">The entries of the menu.</param>
    /// <returns>The title, so that code can add a Class to it.</returns>
    public MenuBarItem AddMenu(string title, IReadOnlyList<MenuItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var item = new MenuBarItem(new ContextMenu { Entries = entries }, title);

        item.Menu.Navigated += (_, direction) => Step(item, direction);

        Children.Add(item);

        return item;
    }

    /// <summary>Opens the menu of a title, closing the menu that was shown before.</summary>
    /// <param name="item">A title of this bar.</param>
    /// <param name="selectFirst"><see langword="true"/> to highlight the first entry, the way a menu opened with the keyboard does.</param>
    /// <returns><see langword="true"/> when the menu opened. It does not when it is open already, has no entries or the bar is not in a window.</returns>
    public bool Open(MenuBarItem item, bool selectFirst = false)
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

            if (selectFirst)
                item.Menu.SelectFirst();

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

    /// <summary>Opens the menu of the title that has the access key that Alt was pressed with.</summary>
    /// <param name="root">The root Control of the window, whose tree is searched for menu bars.</param>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The modifier keys that were held down.</param>
    /// <returns>
    /// <see langword="true"/> when the key was an access key. It opens the menu with its first entry highlighted, or closes it when
    /// it is the open one. Nothing opens while a Dialog or another modal overlay is shown, unless it is a menu of the bar.
    /// </returns>
    /// <example>
    /// <code language="csharp">
    /// if (MenuBar.AccessKeyDown(root, Key.F, KeyModifiers.Alt))
    ///     root.ApplyStyles(resolver);
    /// </code>
    /// </example>
    public static bool AccessKeyDown(Control root, Key key, KeyModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!modifiers.HasFlag(KeyModifiers.Alt) || key.Letter() is not { } letter)
            return false;

        if (FindTitle(root, letter) is not { Bar: { } bar } title)
            return false;

        if (bar.Overlays is { ModalCount: > 0 } && !bar.IsOpen)
            return false;

        return bar.OpenItem == title ? bar.Close() : bar.Open(title, selectFirst: true);
    }

    /// <summary>Finds the first title with an access key in a tree.</summary>
    /// <param name="control">The Control to search in.</param>
    /// <param name="letter">The upper-case letter.</param>
    /// <returns>The title, or <see langword="null"/>.</returns>
    static MenuBarItem? FindTitle(Control control, char letter)
    {
        if (control is MenuBarItem title && title.AccessKey == letter)
            return title;

        foreach (var child in control.Children)
            if (child is Control c && FindTitle(c, letter) is { } found)
                return found;

        return null;
    }

    /// <summary>Moves to the menu of the title next to a title, wrapping around at the ends.</summary>
    /// <param name="item">The title whose menu asked for it.</param>
    /// <param name="direction">-1 for the title before, 1 for the title after.</param>
    void Step(MenuBarItem item, int direction)
    {
        var count = Children.Count;
        var index = (Children.ToList().IndexOf(item) + direction + count) % count;

        if (Children[index] is MenuBarItem next)
            Open(next, selectFirst: true);
    }

    /// <summary>Forgets the open title when its menu was closed by something else, such as Escape or a press on an entry.</summary>
    /// <param name="sender">The overlay layer.</param>
    /// <param name="e">Nothing.</param>
    void OnOverlaysChanged(object? sender, EventArgs e)
    {
        if (current is { Menu.IsOpen: false })
            Close();
    }
}
