using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

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
/// The arrow keys move the highlight, Enter picks the entry, and the access key of an entry (the letter after an ampersand in
/// its text, which is underlined) picks it directly. Right opens the submenu of an entry that has
/// <see cref="MenuItem.Children"/>, which also opens by itself while the entry is highlighted, and Left closes it again.
/// <see cref="MenuItem.Separator"/> entries draw a line and are skipped.
/// </para>
/// <para>
/// A ContextMenu is a ListView, so it is styled with <c>selection-color</c>, <c>selection-text-color</c> and
/// <c>item-height</c> (28px by default) next to the usual box properties, and with <c>separator-color</c> for the lines. It is
/// as wide as its longest entry.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var menu = new ContextMenu
/// {
///     Entries =
///     [
///         new MenuItem("&amp;Copy", Copy),
///         new MenuItem("&amp;Paste", Paste),
///         MenuItem.Separator,
///         new MenuItem("&amp;Recent") { Children = [new MenuItem("a.txt", () => Open("a.txt"))] },
///     ],
/// };
///
/// editor.ContextMenu = menu;
/// </code>
/// </example>
public class ContextMenu : ListView
{
    /// <summary>The room right of the text for the arrow of an entry with a submenu, in pixels.</summary>
    const double ArrowSpace = 16;

    /// <summary>The size of one step of the arrow in pixels.</summary>
    const double ArrowStep = 2;

    /// <summary>The room left of the entries for the check marks, in pixels.</summary>
    const double CheckSpace = 22;

    /// <summary>The room between the text and a shortcut, in pixels.</summary>
    const double ShortcutGap = 24;


    /// <summary>The color of the text of an entry that is off.</summary>
    public static readonly StyledProperty<Color> DisabledColorProperty =
        StyledProperty.Register<ContextMenu, Color>("disabled-text-color", new(156, 163, 175), Color.TryParse);

    /// <summary>The color of the line that a separator entry draws.</summary>
    public static readonly StyledProperty<Color> SeparatorColorProperty =
        StyledProperty.Register<ContextMenu, Color>("separator-color", new(209, 213, 219), Color.TryParse);


    /// <summary>The entries.</summary>
    IReadOnlyList<MenuItem> entries = [];

    /// <summary>The text of each entry without the ampersand, with the index of its access key (or -1) and the key.</summary>
    IReadOnlyList<(string Text, int Index, char? Key)> labels = [];

    /// <summary>Whether any entry has a glyph, which takes the room of an icon.</summary>
    bool hasGlyphs;

    /// <summary>The menu that this menu is a submenu of, or <see langword="null"/>.</summary>
    ContextMenu? parent;

    /// <summary>The submenu that is open.</summary>
    ContextMenu? child;

    /// <summary>The index of the entry that the open submenu belongs to.</summary>
    int childIndex = -1;

    /// <summary>Whether the key that is handled now came from the keyboard, so that a submenu highlights its first entry.</summary>
    bool viaKeyboard;


    /// <summary>
    /// Runs before the first ContextMenu is created, so that the Styled property above is registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ContextMenu()
    {
    }

    /// <summary>Creates an empty menu.</summary>
    public ContextMenu()
    {
        HoverSelects = true;

        Activated += (_, index) => Pick(index);
        SelectionChanged += (_, _) => OpenSelectedSubmenu();
    }


    /// <summary>
    /// Raised when Left or Right is pressed in a menu that cannot use it, with -1 or 1. A <see cref="MenuBar"/> uses it to move
    /// to the menu of the title next to it.
    /// </summary>
    internal event EventHandler<int>? Navigated;


    /// <summary>Gets or sets the entries.</summary>
    public IReadOnlyList<MenuItem> Entries
    {
        get => entries;
        set
        {
            entries = value ?? [];
            labels = entries.Select(entry => AccessKeys.Parse(entry.Text)).ToList();

            Items = labels.Select(label => label.Text).ToList();
            Icons = entries.Select(entry => entry.Icon).ToList();

            hasGlyphs = entries.Any(entry => entry.Glyph is not null);
        }
    }

    /// <summary>Gets whether the menu is shown.</summary>
    public bool IsOpen => Overlays?.Contains(this) == true;

    /// <summary>The menu never takes the keyboard focus, which stays where it was.</summary>
    public override bool IsFocusable => false;

    /// <summary>Gets the room that arrows take right of the text, which is 0 when no entry opens a submenu.</summary>
    double ArrowRoom => entries.Any(entry => entry.Children is not null) ? ArrowSpace : 0;

    /// <summary>Gets the room that check marks take left of the entries, which is 0 when no entry is a check or radio item.</summary>
    double CheckRoom => entries.Any(entry => entry.IsChecked is not null) ? CheckSpace : 0;

    /// <summary>Gets the menu that the chain of submenus started from, which is this menu when it is not a submenu.</summary>
    ContextMenu Root => parent?.Root ?? this;


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

    /// <summary>Closes the menu, and its submenu, without running an entry.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        CloseSubmenu();

        return Overlays?.Close(this) == true;
    }

    /// <summary>Highlights the first entry that can be selected, the way a menu opened with the keyboard does.</summary>
    internal void SelectFirst() => SelectedIndex = FindSelectable(0, 1);

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        switch (key)
        {
            case Key.Right when HasSubmenu(SelectedIndex):
                viaKeyboard = true;
                OpenSubmenu(SelectedIndex);
                viaKeyboard = false;

                return true;

            case Key.Right:
                return Root.Navigate(1);

            case Key.Left when parent is not null:
                return Close();

            case Key.Left:
                return Navigate(-1);
        }

        if (key.Letter() is { } letter && FindAccessKey(letter) is >= 0 and var index)
        {
            viaKeyboard = true;
            SelectedIndex = index;

            Pick(index);

            viaKeyboard = false;

            return true;
        }

        viaKeyboard = true;

        var changed = base.KeyDown(key);

        viaKeyboard = false;

        return changed;
    }

    /// <inheritdoc/>
    protected override bool IsSelectable(int index) => !entries[index].IsSeparator && entries[index].IsEnabled;

    /// <inheritdoc/>
    protected override double IconSpace => hasGlyphs && base.IconSpace == 0 ? IconSize + 6 : base.IconSpace;

    /// <inheritdoc/>
    protected override double RowIndent(int index) => CheckRoom;

    /// <inheritdoc/>
    protected override Color RowTextColor(int index, bool selected) =>
        entries[index].IsEnabled ? base.RowTextColor(index, selected) : GetValue(DisabledColorProperty);

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var width = labels.Select(label => measurer.Measure(label.Text, style).Width).DefaultIfEmpty(0).Max();

        var shortcut = entries
            .Where(entry => entry.ShortcutText is { Length: > 0 })
            .Select(entry => measurer.Measure(entry.ShortcutText!, style).Width)
            .DefaultIfEmpty(0)
            .Max();

        return new Size(
            width + (2 * TextInset) + IconSpace + CheckRoom + ArrowRoom + (shortcut > 0 ? shortcut + ShortcutGap : 0),
            entries.Count * Math.Max(1, GetValue(ItemHeightProperty)));
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);

        context.Save();
        context.ClipRect(content);

        for (var i = 0; i < entries.Count; i++)
            DrawDecoration(context, content, i, RowTop(i));

        context.Restore();
    }

    /// <summary>Draws what a row has on top of its text: a separator line, an arrow, or the underline of its access key.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="content">The content box.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="top">The row's top edge in the menu's own coordinates.</param>
    void DrawDecoration(IDrawingContext context, Rect content, int index, double top)
    {
        if (top + RowHeight < content.Y || top > content.Bottom)
            return;

        var entry = entries[index];
        var color = RowTextColor(index, index == SelectedIndex);

        if (entry.IsSeparator)
        {
            var line = new Rect(content.X + TextInset, top + (RowHeight / 2), content.Width - (2 * TextInset), 1);

            context.FillRect(line, GetValue(SeparatorColorProperty));

            return;
        }

        if (entry.Glyph is { } glyph && entry.Icon is null)
        {
            var square = new Rect(content.X + TextInset + CheckRoom, top + ((RowHeight - IconSize) / 2), IconSize, IconSize);

            Icon.Draw(context, glyph, square, color, 2);
        }

        if (entry.Children is not null)
            DrawArrow(context, content, top, color);

        if (entry.ShortcutText is { Length: > 0 } shortcut)
            DrawShortcut(context, content, top, shortcut, color);

        if (entry.IsChecked == true)
            DrawMark(context, content, top, entry.GroupName is not null, color);

        if (labels[index].Index >= 0)
            DrawUnderline(context, content, index, top, color);
    }

    /// <summary>Draws the arrow of an entry that opens a submenu at the right edge of its row.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="content">The content box.</param>
    /// <param name="top">The row's top edge.</param>
    /// <param name="color">The color of the arrow.</param>
    void DrawArrow(IDrawingContext context, Rect content, double top, Color color)
    {
        var left = content.Right - TextInset - (3 * ArrowStep);
        var middle = top + (RowHeight / 2);

        for (var i = 0; i < 3; i++)
        {
            var length = (5 - (2 * i)) * ArrowStep;

            context.FillRect(new Rect(left + (i * ArrowStep), middle - (length / 2), ArrowStep, length), color);
        }
    }

    /// <summary>Draws the text of a shortcut right-aligned, left of the arrow room.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="content">The content box.</param>
    /// <param name="top">The row's top edge.</param>
    /// <param name="shortcut">The text.</param>
    /// <param name="color">The color of the text.</param>
    void DrawShortcut(IDrawingContext context, Rect content, double top, string shortcut, Color color)
    {
        var style = CurrentTextStyle();
        var size = Measurer().Measure(shortcut, style);

        var x = content.Right - TextInset - ArrowRoom - size.Width;
        var y = top + Math.Max(0, RowHeight - size.Height) / 2;

        context.DrawText(shortcut, x, y, style, color);
    }

    /// <summary>Draws the check mark of a checked check item, or the dot of a checked radio item, in the room left of the text.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="content">The content box.</param>
    /// <param name="top">The row's top edge.</param>
    /// <param name="radio">Whether the entry belongs to a radio group.</param>
    /// <param name="color">The color of the mark.</param>
    void DrawMark(IDrawingContext context, Rect content, double top, bool radio, Color color)
    {
        var size = 12.0;
        var x = content.X + TextInset + ((CheckSpace - size) / 2) - 2;
        var y = top + ((RowHeight - size) / 2);

        if (radio)
        {
            context.FillRect(new Rect(x + 3, y + 3, size - 6, size - 6), color, (size - 6) / 2);

            return;
        }

        var scale = size / Glyphs.Grid * 1.2;
        List<(double X, double Y)> points = [(4, 12), (10, 18), (20, 6)];

        context.StrokePolyline(points.Select(point => (x + (point.X * scale) - 1, y + (point.Y * scale) - 2)).ToList(), color, 1.6);
    }

    /// <summary>Underlines the access key of an entry.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="content">The content box.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="top">The row's top edge.</param>
    /// <param name="color">The color of the line.</param>
    void DrawUnderline(IDrawingContext context, Rect content, int index, double top, Color color)
    {
        var (text, at, _) = labels[index];
        var style = CurrentTextStyle();
        var measurer = Measurer();

        var before = measurer.Measure(text[..at], style).Width;
        var key = measurer.Measure(text[at].ToString(), style);

        var x = content.X + TextInset + CheckRoom + IconSpace + before;
        var y = top + Math.Max(0, RowHeight - key.Height) / 2;

        context.FillRect(new Rect(x, y + key.Height - 1, key.Width, 1), color);
    }

    /// <summary>Tells whether an entry opens a submenu.</summary>
    /// <param name="index">The entry's index, or -1 for none.</param>
    /// <returns><see langword="true"/> when the entry has entries of its own.</returns>
    bool HasSubmenu(int index) => index >= 0 && entries[index].Children is { Count: > 0 };

    /// <summary>Finds the entry that has an access key.</summary>
    /// <param name="letter">The upper-case letter.</param>
    /// <returns>The entry's index, or -1.</returns>
    int FindAccessKey(char letter)
    {
        for (var i = 0; i < labels.Count; i++)
            if (IsSelectable(i) && labels[i].Key == letter)
                return i;

        return -1;
    }

    /// <summary>Runs after the highlight moved: closes the submenu that was open and opens the one of the new entry.</summary>
    void OpenSelectedSubmenu()
    {
        CloseSubmenu();

        if (HasSubmenu(SelectedIndex))
            OpenSubmenu(SelectedIndex);
    }

    /// <summary>Opens the submenu of an entry to the right of its row, unless it is open already.</summary>
    /// <param name="index">The entry's index.</param>
    void OpenSubmenu(int index)
    {
        if (child is { IsOpen: true } && childIndex == index)
            return;

        CloseSubmenu();

        var (originX, originY) = ToLocal(0, 0);
        var sub = new ContextMenu { Entries = entries[index].Children ?? [], parent = this };

        if (!sub.Show(this, Bounds.Width - originX, RowTop(index) - originY))
            return;

        child = sub;
        childIndex = index;

        if (viaKeyboard)
            sub.SelectedIndex = sub.FindSelectable(0, 1);
    }

    /// <summary>Closes the submenu that is open.</summary>
    void CloseSubmenu()
    {
        var sub = child;

        child = null;
        childIndex = -1;

        sub?.Close();
    }

    /// <summary>Asks the owner of the menu to move to the menu next to it.</summary>
    /// <param name="direction">-1 for the one before, 1 for the one after.</param>
    /// <returns><see langword="true"/> when something listened.</returns>
    bool Navigate(int direction)
    {
        Navigated?.Invoke(this, direction);

        return Navigated is not null;
    }

    /// <summary>Runs an entry, or opens its submenu, and closes the menu and its submenus when an entry ran.</summary>
    /// <param name="index">The entry's index.</param>
    void Pick(int index)
    {
        var entry = entries[index];

        if (entry.IsSeparator || !entry.IsEnabled)
            return;

        if (HasSubmenu(index))
        {
            OpenSubmenu(index);

            return;
        }

        Check(entry);

        Root.Close();

        entry.Click?.Invoke();
    }

    /// <summary>Updates the check state of an entry that was picked: a check item flips, a radio item turns on and the others of its group off.</summary>
    /// <param name="entry">The entry.</param>
    void Check(MenuItem entry)
    {
        if (entry.IsChecked is not { } on)
            return;

        if (entry.GroupName is null)
        {
            entry.IsChecked = !on;

            return;
        }

        foreach (var other in entries.Where(other => other.GroupName == entry.GroupName && other.IsChecked is not null))
            other.IsChecked = ReferenceEquals(other, entry);
    }
}
