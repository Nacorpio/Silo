using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A scrolling list of text rows from which the user selects one. Only the rows that are visible are drawn, so it stays fast
/// with a very long list.
/// </summary>
/// <remarks>
/// <para>
/// Every row has the height <c>item-height</c> (28px by default), so the visible rows follow from the scroll offset without
/// looking at the others. A ListView takes the keyboard focus when it is clicked, and then moves the selection with the up and
/// down arrows, Home and End. The mouse wheel scrolls it. Give it a <c>height</c>, since it has no content to size itself from.
/// </para>
/// <para>
/// The selected row is painted with <c>selection-color</c> and <c>selection-text-color</c>. Rows are text for now, with an
/// optional icon in front: set <see cref="Icons"/> to one <see cref="IImage"/> per row, <see langword="null"/> for a row
/// without. The icons are scaled to fit a square of <c>icon-size</c> (16px by default). An icon that is a mask
/// (<see cref="IImage.IsMask"/>) is drawn in the text color of its row, so it turns white on the selected row. Rows made of Controls arrive with
/// templates in Silo.Views.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var files = new ListView { Name = "files" };
///
/// files.Items = Directory.GetFiles(path).Select(Path.GetFileName).ToList();
/// files.Icons = Directory.GetFiles(path).Select(file => IconFor(file)).ToList();
/// files.SelectionChanged += (_, _) => Open(files.Items[files.SelectedIndex]);
/// </code>
/// </example>
public class ListView : Control, IScrollable, IPointerHandler
{
    /// <summary>The space left of each row's text in pixels.</summary>
    internal const double TextInset = 8;

    /// <summary>The space between a row's icon and its text in pixels.</summary>
    internal const double IconGap = 8;


    /// <summary>The height of each row in pixels.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        StyledProperty.Register<ListView, double>("item-height", 28, ValueParsers.Pixels);

    /// <summary>The side of the square that each row's icon is scaled to fit, in pixels.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        StyledProperty.Register<ListView, double>("icon-size", 16, ValueParsers.Pixels);

    /// <summary>The background of the selected row.</summary>
    public static readonly StyledProperty<Color> SelectionColorProperty =
        StyledProperty.Register<ListView, Color>("selection-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The text color of the selected row.</summary>
    public static readonly StyledProperty<Color> SelectionTextColorProperty =
        StyledProperty.Register<ListView, Color>("selection-text-color", new(255, 255, 255), Color.TryParse);


    /// <summary>The rows.</summary>
    IReadOnlyList<string> items = [];

    /// <summary>The icons of the rows.</summary>
    IReadOnlyList<IImage?> icons = [];

    /// <summary>Whether any row has an icon.</summary>
    bool hasIcons;

    /// <summary>The index of the selected row, or -1.</summary>
    int selectedIndex = -1;

    /// <summary>How far the rows are scrolled up.</summary>
    readonly ScrollState scroll;


    /// <summary>
    /// Runs before the first ListView is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ListView()
    {
    }

    /// <summary>Creates an empty ListView.</summary>
    public ListView() => scroll = new ScrollState(() => items.Count * RowHeight, () => Viewport.Height);

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Raised when the user picks a row: by pressing the pointer on it, or Enter on the selected row. Unlike
    /// <see cref="SelectionChanged"/> it is also raised for the row that is already selected. The argument is the row's index.
    /// </summary>
    public event EventHandler<int>? Activated;

    /// <summary>Gets or sets the rows. Setting it clears the selection and scrolls to the top.</summary>
    public IReadOnlyList<string> Items
    {
        get => items;
        set
        {
            items = value ?? [];
            scroll.Reset();

            SelectedIndex = -1;
        }
    }

    /// <summary>
    /// Gets or sets the icons: <c>Icons[i]</c> is shown in front of <c>Items[i]</c>. Rows beyond the end of the list, and
    /// <see langword="null"/> entries, have no icon. When no row has one, the text starts at the left edge as usual,
    /// otherwise every row's text is moved right to line up.
    /// </summary>
    public IReadOnlyList<IImage?> Icons
    {
        get => icons;
        set
        {
            icons = value ?? [];
            hasIcons = icons.Any(icon => icon is not null);
        }
    }

    /// <summary>Gets or sets the index of the selected row, or -1 for none. Values outside the list select nothing.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set => Select(value, true);
    }

    /// <summary>
    /// Gets or sets whether the row under the pointer is selected as the pointer moves over it, the way menus work. The
    /// default is <see langword="false"/>.
    /// </summary>
    public bool HoverSelects { get; set; }

    /// <summary>Gets how far the rows are scrolled, in pixels. It is between 0 and <see cref="MaxOffset"/>.</summary>
    public double Offset => scroll.Offset;

    /// <summary>Gets the largest <see cref="Offset"/>: how much taller the rows are than the viewport.</summary>
    /// <remarks>It is only meaningful after <see cref="Control.Arrange"/>.</remarks>
    public double MaxOffset => scroll.MaxOffset;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the room that the icons take in front of the text in pixels, which is 0 when no row has an icon.</summary>
    protected virtual double IconSpace => hasIcons ? IconSize + IconGap : 0;

    /// <summary>Gets the side of the square that the icons fit in, in pixels.</summary>
    protected double IconSize => Math.Max(0, GetValue(IconSizeProperty));

    /// <summary>Gets the height of a row in pixels.</summary>
    protected double RowHeight => Math.Max(1, GetValue(ItemHeightProperty));

    /// <summary>Gets the visible area in the ListView's own coordinates.</summary>
    protected Rect Viewport => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <inheritdoc/>
    public bool ScrollBy(double delta) => scroll.ScrollBy(delta);

    /// <summary>Selects the row under a moving pointer when <see cref="HoverSelects"/> is set. The list does not scroll.</summary>
    /// <param name="e">The pointer position relative to the ListView's border box.</param>
    /// <returns><see langword="true"/> when the selection changed, so a new frame is needed.</returns>
    public bool PointerMove(PointerEvent e)
    {
        var before = selectedIndex;

        if (HoverSelects)
            Select((int)Math.Floor((e.Y - Viewport.Y + Offset) / RowHeight), false);

        return before != selectedIndex;
    }

    /// <summary>Selects the row at a pointer position.</summary>
    /// <param name="e">The pointer position relative to the ListView's border box.</param>
    /// <returns><see langword="true"/> when the selection changed, so a new frame is needed.</returns>
    public virtual bool PointerDown(PointerEvent e)
    {
        var before = selectedIndex;

        SelectedIndex = (int)Math.Floor((e.Y - Viewport.Y + Offset) / RowHeight);

        if (selectedIndex >= 0)
            Activated?.Invoke(this, selectedIndex);

        return before != selectedIndex;
    }

    /// <summary>Gets the top edge of a row in the ListView's own coordinates, which is outside the viewport for a row that is scrolled away.</summary>
    /// <param name="index">The row's index.</param>
    /// <returns>The distance from the top of the border box.</returns>
    protected double RowTop(int index) => Viewport.Y + (index * RowHeight) - Offset;

    /// <summary>Gets how far a row's icon and text are moved right, in pixels. A derived class overrides it to indent rows.</summary>
    /// <param name="index">The row's index.</param>
    /// <returns>The distance, 0 by default.</returns>
    protected virtual double RowIndent(int index) => 0;

    /// <summary>Tells whether a row can be selected. A row that cannot is skipped by the arrow keys and ignores the pointer.</summary>
    /// <param name="index">The row's index.</param>
    /// <returns><see langword="true"/> unless a derived class says otherwise.</returns>
    protected virtual bool IsSelectable(int index) => true;

    /// <summary>Finds the first row that can be selected, starting at a row and moving one way.</summary>
    /// <param name="from">The row to start at, which is included.</param>
    /// <param name="step">1 to move down or -1 to move up.</param>
    /// <returns>The row's index, or -1 when there is none.</returns>
    protected int FindSelectable(int from, int step)
    {
        for (var i = from; i >= 0 && i < items.Count; i += step)
            if (IsSelectable(i))
                return i;

        return -1;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = selectedIndex;

        switch (key)
        {
            case Key.Up:
                Move(selectedIndex < 0 ? FindSelectable(0, 1) : FindSelectable(selectedIndex - 1, -1));
                break;

            case Key.Down:
                Move(FindSelectable(selectedIndex + 1, 1));
                break;

            case Key.Home:
                Move(FindSelectable(0, 1));
                break;

            case Key.End:
                Move(FindSelectable(items.Count - 1, -1));
                break;

            case Key.Enter when selectedIndex >= 0:
                Activated?.Invoke(this, selectedIndex);
                break;
        }

        return before != selectedIndex;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var height = RowHeight;
        var first = (int)Math.Floor(Offset / height);
        var last = Math.Min(items.Count - 1, (int)Math.Floor((Offset + content.Height) / height));

        context.Save();
        context.ClipRect(content);

        for (var i = first; i <= last; i++)
            DrawRow(context, content, i, content.Y + i * height - Offset);

        context.Restore();
    }

    /// <summary>Draws one row.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="content">The content box.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="top">The row's top edge in the ListView's own coordinates.</param>
    void DrawRow(IDrawingContext context, Rect content, int index, double top)
    {
        var selected = index == selectedIndex;
        var style = CurrentTextStyle();
        var color = RowTextColor(index, selected);

        if (selected)
            context.FillRect(new Rect(content.X, top, content.Width, RowHeight), GetValue(SelectionColorProperty));

        var line = Measurer().Measure(items[index], style);
        var y = top + Math.Max(0, RowHeight - line.Height) / 2;

        context.DrawText(items[index], content.X + TextInset + RowIndent(index) + IconSpace, y, style, color);

        if (index < icons.Count && icons[index] is { } icon)
            DrawIcon(context, icon, content.X + TextInset + RowIndent(index), top, color);
    }

    /// <summary>Gets the color that a row's text and icon are drawn in.</summary>
    /// <param name="index">The row's index.</param>
    /// <param name="selected">Whether the row is selected.</param>
    /// <returns>The selection text color for a selected row, otherwise the text color.</returns>
    protected virtual Color RowTextColor(int index, bool selected) => GetValue(selected ? SelectionTextColorProperty : ColorProperty);

    /// <summary>Draws a row's icon, centered vertically in the row.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="x">The left edge of the icon's square.</param>
    /// <param name="top">The row's top edge.</param>
    /// <param name="color">The text color of the row, which a mask icon is drawn in.</param>
    void DrawIcon(IDrawingContext context, IImage icon, double x, double top, Color color)
    {
        var square = new Rect(x, top + (RowHeight - IconSize) / 2, IconSize, IconSize);
        var destination = Image.Fit(icon, square);

        if (destination.Width > 0)
            Image.Draw(context, icon, destination, color);
    }

    /// <summary>Moves the selection to a row that was found with <see cref="FindSelectable"/>.</summary>
    /// <param name="index">The row's index, or -1 to leave the selection where it is.</param>
    void Move(int index)
    {
        if (index >= 0)
            SelectedIndex = index;
    }

    /// <summary>Changes the selection.</summary>
    /// <param name="value">The row's index. Values outside the list, and rows that cannot be selected, select nothing.</param>
    /// <param name="reveal">Whether to scroll the row into view.</param>
    void Select(int value, bool reveal)
    {
        var next = value >= 0 && value < items.Count && IsSelectable(value) ? value : -1;

        if (next == selectedIndex)
            return;

        selectedIndex = next;

        if (reveal)
            Reveal(next);

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Scrolls just enough to show a row.</summary>
    /// <param name="index">The row's index, or -1 for none.</param>
    void Reveal(int index)
    {
        if (index >= 0)
            scroll.EnsureVisible(index * RowHeight, RowHeight);
    }
}
