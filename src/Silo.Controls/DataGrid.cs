using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A table: a header row and a scrolling list of rows, with a text cell for every column.
/// </summary>
/// <remarks>
/// <para>
/// The columns are <see cref="DataGridColumn"/>s that read their cell from the row objects of <see cref="Rows"/>. The grid
/// paints only the rows that are in view, so a long list costs no more than a short one. Mouse wheel scrolling goes through
/// <see cref="IScrollable"/>, and when the columns are wider than the grid, Left and Right scroll sideways.
/// </para>
/// <para>
/// Pressing a header sorts by its column, first ascending, then descending, then not at all, and shows an arrow in it; the
/// <see cref="Rows"/> list itself is not changed. Dragging the right edge of a header resizes its column. Pressing a row
/// selects it, and a second press on it, or Enter, raises <see cref="RowActivated"/>. Up, Down, PageUp, PageDown, Home and
/// End move the selection.
/// </para>
/// <para>
/// The look comes from styled properties, since the grid draws its parts itself: <c>row-height</c>, <c>header-height</c>,
/// <c>header-background</c>, <c>header-color</c>, <c>grid-color</c> (the lines between rows and columns),
/// <c>alternate-row-color</c> (zebra stripes, transparent by default), <c>hover-color</c>, <c>selection-color</c>,
/// <c>selection-text-color</c>, and <c>icon-size</c> and <c>icon-gap</c> for the icons of the columns. Give it a <c>height</c>, since it has no content to size itself from.
/// </para>
/// <para>
/// A column can have an <see cref="DataGridColumn.Icon"/> in its header and an <see cref="DataGridColumn.IconBinding"/> for the
/// icons in its cells. They stand in front of the text and are scaled to fit a square of <c>icon-size</c>; an icon that is a
/// mask is drawn in the text color of its cell, so it follows the selection.
/// </para>
/// <para>The <see cref="PointerRouter"/> drives it through <see cref="IPointerHandler"/> and <see cref="PointerMove"/>.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var grid = new DataGrid { Rows = people };
///
/// grid.Columns.Add(new DataGridColumn("Name", row => ((Person)row).Name) { Star = 1 });
/// grid.Columns.Add(new DataGridColumn("Age", row => ((Person)row).Age) { Width = 60, Alignment = TextAlign.Right });
///
/// grid.RowActivated += (_, index) => Open((Person)grid.SelectedRow!);
/// </code>
/// </example>
public class DataGrid : Control, IScrollable, IPointerHandler
{
    /// <summary>The space left and right of the text of a cell, in pixels.</summary>
    const double CellInset = 8;

    /// <summary>The distance from the right edge of a header in which a press starts resizing, in pixels.</summary>
    const double ResizeZone = 4;

    /// <summary>The room at the right edge of a sorted header for the arrow, in pixels.</summary>
    const double ArrowSpace = 18;

    /// <summary>How far Left and Right scroll sideways, in pixels.</summary>
    const double HorizontalStep = 40;

    /// <summary>The longest time between the two presses of a double press, in milliseconds.</summary>
    const double DoublePressTime = 400;

    /// <summary>The height of a row in pixels.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        StyledProperty.Register<DataGrid, double>("row-height", 28, ValueParsers.Pixels);

    /// <summary>The height of the header row in pixels.</summary>
    public static readonly StyledProperty<double> HeaderHeightProperty =
        StyledProperty.Register<DataGrid, double>("header-height", 30, ValueParsers.Pixels);

    /// <summary>The width and height of the icons of the columns in pixels.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        StyledProperty.Register<DataGrid, double>("icon-size", 16, ValueParsers.Pixels);

    /// <summary>The space between an icon and the text in pixels.</summary>
    public static readonly StyledProperty<double> IconGapProperty =
        StyledProperty.Register<DataGrid, double>("icon-gap", 6, ValueParsers.Pixels);

    /// <summary>The background of the header row.</summary>
    public static readonly StyledProperty<Color> HeaderBackgroundProperty =
        StyledProperty.Register<DataGrid, Color>("header-background", new(243, 244, 246), Color.TryParse);

    /// <summary>The color of the header text and of its sort arrow.</summary>
    public static readonly StyledProperty<Color> HeaderColorProperty =
        StyledProperty.Register<DataGrid, Color>("header-color", new(55, 65, 81), Color.TryParse);

    /// <summary>The color of the lines between rows and columns.</summary>
    public static readonly StyledProperty<Color> GridColorProperty =
        StyledProperty.Register<DataGrid, Color>("grid-color", new(229, 231, 235), Color.TryParse);

    /// <summary>The background of every second row. It is transparent by default, so there are no stripes.</summary>
    public static readonly StyledProperty<Color> AlternateRowColorProperty =
        StyledProperty.Register<DataGrid, Color>("alternate-row-color", new(0, 0, 0, 0), Color.TryParse);

    /// <summary>The background of the row under the pointer. It is transparent by default.</summary>
    public static readonly StyledProperty<Color> HoverColorProperty =
        StyledProperty.Register<DataGrid, Color>("hover-color", new(0, 0, 0, 0), Color.TryParse);

    /// <summary>The background of the selected row.</summary>
    public static readonly StyledProperty<Color> SelectionColorProperty =
        StyledProperty.Register<DataGrid, Color>("selection-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The text color of the selected row.</summary>
    public static readonly StyledProperty<Color> SelectionTextColorProperty =
        StyledProperty.Register<DataGrid, Color>("selection-text-color", new(255, 255, 255), Color.TryParse);


    /// <summary>The rows in the order of the source.</summary>
    IReadOnlyList<object> rows = [];

    /// <summary>The indexes into <see cref="rows"/> in the order they are shown.</summary>
    int[] order = [];

    /// <summary>The column the grid is sorted by.</summary>
    DataGridColumn? sortColumn;

    /// <summary>Whether the sort is descending.</summary>
    bool sortDescending;

    /// <summary>The shown index of the selected row.</summary>
    int selectedIndex = -1;

    /// <summary>The shown index of the row under the pointer.</summary>
    int hoveredIndex = -1;

    /// <summary>How far the rows are scrolled down in pixels.</summary>
    double offset;

    /// <summary>How far the columns are scrolled to the left in pixels.</summary>
    double horizontalOffset;

    /// <summary>The column whose edge is being dragged.</summary>
    DataGridColumn? resizing;

    /// <summary>The pointer position and column width when the drag started.</summary>
    (double X, double Width) resizeStart;

    /// <summary>The shown index of the row that was pressed last, and when.</summary>
    (int Index, DateTimeOffset At) lastPress = (-1, DateTimeOffset.MinValue);


    /// <summary>
    /// Runs before the first DataGrid is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static DataGrid()
    {
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when a selected row is pressed again or Enter is pressed. The argument is the shown index of the row.</summary>
    public event EventHandler<int>? RowActivated;

    /// <summary>Raised after the sort changed, by a press on a header or by <see cref="SortBy"/>.</summary>
    public event EventHandler? SortChanged;

    /// <summary>Gets the columns, in the order they are shown. Add to it before the grid is shown.</summary>
    public List<DataGridColumn> Columns { get; } = [];

    /// <summary>Gets or sets the clock that tells a double press. It is only replaced in tests.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Gets or sets the rows. Setting it clears the selection and scrolls back to the top, and keeps the sort.</summary>
    /// <remarks>Changes to the list afterwards are not noticed: set it again.</remarks>
    public IReadOnlyList<object> Rows
    {
        get => rows;
        set
        {
            rows = value ?? [];
            offset = 0;

            Reorder();

            SelectedIndex = -1;
        }
    }

    /// <summary>Gets the column the rows are sorted by, or <see langword="null"/> when they are in the order of the source.</summary>
    public DataGridColumn? SortColumn => sortColumn;

    /// <summary>Gets whether the sort is descending.</summary>
    public bool SortDescending => sortDescending;

    /// <summary>Gets or sets the shown index of the selected row, or -1 for none. An index out of range selects none.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set => Select(value, reveal: true);
    }

    /// <summary>Gets the selected row, or <see langword="null"/> when none is selected.</summary>
    public object? SelectedRow => selectedIndex >= 0 ? rows[order[selectedIndex]] : null;

    /// <summary>Gets how far the rows are scrolled down in pixels.</summary>
    public double Offset => Math.Clamp(offset, 0, MaxOffset);

    /// <summary>Gets how far the rows can be scrolled down, in pixels.</summary>
    public double MaxOffset => Math.Max(0, (order.Length * RowHeight) - BodyHeight);

    /// <summary>Gets how far the columns are scrolled to the left in pixels.</summary>
    public double HorizontalOffset => Math.Clamp(horizontalOffset, 0, MaxHorizontalOffset);

    /// <summary>Gets how far the columns can be scrolled to the left, in pixels. It is 0 while they fit.</summary>
    public double MaxHorizontalOffset => Math.Max(0, ColumnWidths().Sum() - Viewport.Width);

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the widths of the columns in pixels, as they are shown.</summary>
    /// <returns>One width for every column of <see cref="Columns"/>.</returns>
    public IReadOnlyList<double> ColumnWidths()
    {
        var fixedWidth = Columns.Where(c => c.Star <= 0).Sum(c => Math.Max(c.MinWidth, c.Width));
        var stars = Columns.Where(c => c.Star > 0).Sum(c => c.Star);
        var remaining = Math.Max(0, Viewport.Width - fixedWidth);

        return Columns
            .Select(c => c.Star > 0 ? Math.Max(c.MinWidth, remaining * c.Star / stars) : Math.Max(c.MinWidth, c.Width))
            .ToList();
    }

    /// <summary>Sorts by a column.</summary>
    /// <param name="column">The column, or <see langword="null"/> to show the rows in the order of the source.</param>
    /// <param name="descending">Whether to sort from the largest value to the smallest.</param>
    /// <exception cref="ArgumentException"><paramref name="column"/> is not one of <see cref="Columns"/>.</exception>
    public void SortBy(DataGridColumn? column, bool descending = false)
    {
        if (column is not null && !Columns.Contains(column))
            throw new ArgumentException("The column is not one of the Columns of the grid.", nameof(column));

        var selected = SelectedRow;

        (sortColumn, sortDescending) = (column, column is not null && descending);

        Reorder();

        selectedIndex = selected is null ? -1 : Array.FindIndex(order, i => ReferenceEquals(rows[i], selected));

        SortChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var before = Offset;

        offset = Math.Clamp(before + delta, 0, MaxOffset);

        return offset != before;
    }

    /// <summary>Scrolls the columns sideways.</summary>
    /// <param name="delta">The distance in pixels. Positive scrolls to the right.</param>
    /// <returns><see langword="true"/> when the columns moved, so a new frame is needed.</returns>
    public bool ScrollHorizontallyBy(double delta)
    {
        var before = HorizontalOffset;

        horizontalOffset = Math.Clamp(before + delta, 0, MaxHorizontalOffset);

        return horizontalOffset != before;
    }

    /// <summary>Highlights the row under the pointer.</summary>
    /// <param name="e">The pointer position relative to the grid's border box.</param>
    /// <returns><see langword="true"/> when the highlighted row changed, so a new frame is needed.</returns>
    public bool PointerMove(PointerEvent e)
    {
        var before = hoveredIndex;

        hoveredIndex = RowAt(e.Y);

        return before != hoveredIndex;
    }

    /// <summary>Takes the highlight away, when the pointer left the grid.</summary>
    /// <returns><see langword="true"/> when a row was highlighted, so a new frame is needed.</returns>
    public bool PointerLeave()
    {
        var before = hoveredIndex;

        hoveredIndex = -1;

        return before != -1;
    }

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        if (e.Y < Viewport.Y + HeaderHeight)
            return PressHeader(e.X);

        var index = RowAt(e.Y);
        var before = selectedIndex;
        var now = TimeProvider.GetUtcNow();
        var again = index >= 0 && index == lastPress.Index && (now - lastPress.At).TotalMilliseconds <= DoublePressTime;

        SelectedIndex = index;

        lastPress = again ? (-1, DateTimeOffset.MinValue) : (index, now);

        if (again)
            RowActivated?.Invoke(this, index);

        return before != selectedIndex;
    }

    /// <inheritdoc/>
    public bool PointerDrag(PointerEvent e)
    {
        if (resizing is not { } column)
            return false;

        var width = Math.Max(column.MinWidth, resizeStart.Width + (e.X - resizeStart.X));

        if (width == column.Width && column.Star <= 0)
            return false;

        (column.Width, column.Star) = (width, 0);

        return true;
    }

    /// <inheritdoc/>
    public bool PointerUp(PointerEvent e)
    {
        resizing = null;

        return false;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = selectedIndex;
        var page = Math.Max(1, (int)(BodyHeight / RowHeight));
        var last = order.Length - 1;

        switch (key)
        {
            case Key.Up:
                SelectedIndex = selectedIndex < 0 ? 0 : Math.Max(0, selectedIndex - 1);
                break;

            case Key.Down:
                SelectedIndex = Math.Min(last, selectedIndex + 1);
                break;

            case Key.PageUp:
                SelectedIndex = Math.Max(0, selectedIndex - page);
                break;

            case Key.PageDown:
                SelectedIndex = Math.Min(last, Math.Max(0, selectedIndex) + page);
                break;

            case Key.Home:
                SelectedIndex = 0;
                break;

            case Key.End:
                SelectedIndex = last;
                break;

            case Key.Left:
                return ScrollHorizontallyBy(-HorizontalStep);

            case Key.Right:
                return ScrollHorizontallyBy(HorizontalStep);

            case Key.Enter when selectedIndex >= 0:
                RowActivated?.Invoke(this, selectedIndex);
                break;
        }

        return before != selectedIndex;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var body = new Rect(content.X, content.Y + HeaderHeight, content.Width, Math.Max(0, content.Height - HeaderHeight));
        var widths = ColumnWidths();
        var grid = GetValue(GridColorProperty);
        var left = content.X - HorizontalOffset;

        context.Save();
        context.ClipRect(body);

        var first = (int)Math.Floor(Offset / RowHeight);
        var last = Math.Min(order.Length - 1, (int)Math.Floor((Offset + body.Height) / RowHeight));

        for (var i = first; i <= last; i++)
            DrawRow(context, body, i, body.Y + (i * RowHeight) - Offset, left, widths);

        DrawColumnLines(context, body, left, widths, grid);

        context.Restore();

        DrawHeader(context, new Rect(content.X, content.Y, content.Width, HeaderHeight), left, widths, grid);
    }

    /// <summary>Gets the area inside the border and padding.</summary>
    Rect Viewport => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <summary>Gets the height of a row in pixels.</summary>
    double RowHeight => Math.Max(1, GetValue(RowHeightProperty));

    /// <summary>Gets the height of the header in pixels.</summary>
    double HeaderHeight => Math.Max(0, GetValue(HeaderHeightProperty));

    /// <summary>Gets the height of the area below the header in pixels.</summary>
    double BodyHeight => Math.Max(0, Viewport.Height - HeaderHeight);

    /// <summary>Gets the shown index of the row at a vertical position.</summary>
    /// <param name="y">The position relative to the grid's border box.</param>
    /// <returns>The index, or -1 when the position is not on a row.</returns>
    int RowAt(double y)
    {
        var index = (int)Math.Floor((y - Viewport.Y - HeaderHeight + Offset) / RowHeight);

        return y >= Viewport.Y + HeaderHeight && index >= 0 && index < order.Length ? index : -1;
    }

    /// <summary>Handles a press on the header: it starts resizing at an edge, and sorts elsewhere.</summary>
    /// <param name="x">The horizontal position relative to the grid's border box.</param>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    bool PressHeader(double x)
    {
        var widths = ColumnWidths();
        var right = Viewport.X - HorizontalOffset;

        for (var i = 0; i < Columns.Count; i++)
        {
            right += widths[i];

            if (Math.Abs(x - right) <= ResizeZone)
            {
                resizing = Columns[i];
                resizeStart = (x, widths[i]);

                return false;
            }

            if (x >= right - widths[i] && x < right)
                return Toggle(Columns[i]);
        }

        return false;
    }

    /// <summary>Moves a column to the next sort: ascending, descending, none.</summary>
    /// <param name="column">The column whose header was pressed.</param>
    /// <returns>Always <see langword="true"/>, since the arrow changes.</returns>
    bool Toggle(DataGridColumn column)
    {
        if (sortColumn != column)
            SortBy(column);
        else if (!sortDescending)
            SortBy(column, descending: true);
        else
            SortBy(null);

        return true;
    }

    /// <summary>Orders the rows by the sort.</summary>
    void Reorder()
    {
        var indexes = Enumerable.Range(0, rows.Count);

        if (sortColumn is not { } column)
        {
            order = [.. indexes];

            return;
        }

        var comparer = new SafeComparer(column.Comparer);

        order = sortDescending
            ? [.. indexes.OrderByDescending(i => column.Binding(rows[i]), comparer)]
            : [.. indexes.OrderBy(i => column.Binding(rows[i]), comparer)];
    }

    /// <summary>Selects a row.</summary>
    /// <param name="value">The shown index.</param>
    /// <param name="reveal">Whether to scroll so that the row is in view.</param>
    void Select(int value, bool reveal)
    {
        var next = value >= 0 && value < order.Length ? value : -1;

        if (next == selectedIndex)
            return;

        selectedIndex = next;

        if (reveal)
            Reveal(next);

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Scrolls the least that brings a row into view.</summary>
    /// <param name="index">The shown index.</param>
    void Reveal(int index)
    {
        var height = BodyHeight;

        if (index < 0 || height <= 0)
            return;

        var top = index * RowHeight;

        if (top < offset)
        {
            offset = top;

            return;
        }

        offset = Math.Max(offset, top + RowHeight - height);
    }

    /// <summary>Draws one row.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="body">The area of the rows.</param>
    /// <param name="index">The shown index.</param>
    /// <param name="top">The top edge of the row.</param>
    /// <param name="left">The left edge of the first column.</param>
    /// <param name="widths">The widths of the columns.</param>
    void DrawRow(IDrawingContext context, Rect body, int index, double top, double left, IReadOnlyList<double> widths)
    {
        var selected = index == selectedIndex;
        var row = new Rect(body.X, top, body.Width, RowHeight);
        var background = selected ? GetValue(SelectionColorProperty)
            : index == hoveredIndex ? GetValue(HoverColorProperty)
            : index % 2 == 1 ? GetValue(AlternateRowColorProperty)
            : new Color(0, 0, 0, 0);

        if (background.A > 0)
            context.FillRect(row, background);

        context.FillRect(new Rect(row.X, row.Bottom - 1, row.Width, 1), GetValue(GridColorProperty));

        var source = rows[order[index]];
        var color = GetValue(selected ? SelectionTextColorProperty : ColorProperty);
        var x = left;

        for (var i = 0; i < Columns.Count; i++)
        {
            var icon = Columns[i].IconBinding?.Invoke(source);

            DrawCell(context, Columns[i].TextOf(source), icon, new Rect(x, top, widths[i], RowHeight), Columns[i].Alignment, color, CurrentTextStyle());

            x += widths[i];
        }
    }

    /// <summary>Draws the icon and the text of a cell or header, clipped to it.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="text">The text.</param>
    /// <param name="icon">The icon in front of the text, or <see langword="null"/>.</param>
    /// <param name="cell">The area of the cell.</param>
    /// <param name="alignment">Where the icon and the text stand in it, as one.</param>
    /// <param name="color">The text color, which also fills a mask icon.</param>
    /// <param name="style">The font.</param>
    void DrawCell(IDrawingContext context, string text, IImage? icon, Rect cell, TextAlign alignment, Color color, TextStyle style)
    {
        var size = Measurer().Measure(text, style);
        var room = Math.Max(0, cell.Width - (2 * CellInset));
        var iconSize = Math.Max(0, GetValue(IconSizeProperty));
        var iconSpace = icon is null ? 0 : iconSize + Math.Max(0, GetValue(IconGapProperty));
        var spare = Math.Max(0, room - iconSpace - size.Width);

        var x = alignment switch
        {
            TextAlign.Right => cell.X + CellInset + spare,
            TextAlign.Center => cell.X + CellInset + (spare / 2),
            _ => cell.X + CellInset,
        };

        context.Save();
        context.ClipRect(new Rect(cell.X + CellInset, cell.Y, room, cell.Height));

        if (icon is not null)
        {
            var square = new Rect(x, cell.Y + ((cell.Height - iconSize) / 2), iconSize, iconSize);
            var destination = Image.Fit(icon, square);

            if (destination.Width > 0)
                Image.Draw(context, icon, destination, color);
        }

        context.DrawText(text, x + iconSpace, cell.Y + (Math.Max(0, cell.Height - size.Height) / 2), style, color);
        context.Restore();
    }

    /// <summary>Draws the lines between the columns.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="body">The area of the rows.</param>
    /// <param name="left">The left edge of the first column.</param>
    /// <param name="widths">The widths of the columns.</param>
    /// <param name="color">The color of the lines.</param>
    static void DrawColumnLines(IDrawingContext context, Rect body, double left, IReadOnlyList<double> widths, Color color)
    {
        var x = left;

        foreach (var width in widths)
        {
            x += width;

            context.FillRect(new Rect(x - 1, body.Y, 1, body.Height), color);
        }
    }

    /// <summary>Draws the header row.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="header">The area of the header.</param>
    /// <param name="left">The left edge of the first column.</param>
    /// <param name="widths">The widths of the columns.</param>
    /// <param name="grid">The color of the lines.</param>
    void DrawHeader(IDrawingContext context, Rect header, double left, IReadOnlyList<double> widths, Color grid)
    {
        var color = GetValue(HeaderColorProperty);
        var style = CurrentTextStyle() with { Bold = true };
        var x = left;

        context.Save();
        context.ClipRect(header);
        context.FillRect(header, GetValue(HeaderBackgroundProperty));
        context.FillRect(new Rect(header.X, header.Bottom - 1, header.Width, 1), grid);

        for (var i = 0; i < Columns.Count; i++)
        {
            var cell = new Rect(x, header.Y, widths[i], header.Height);

            var sorted = Columns[i] == sortColumn;
            var label = sorted ? cell with { Width = Math.Max(0, cell.Width - ArrowSpace) } : cell;

            DrawCell(context, Columns[i].Header, Columns[i].Icon, label, Columns[i].Alignment, color, style);

            if (sorted)
                DrawArrow(context, new Rect(cell.Right - ArrowSpace, cell.Y, 12, cell.Height), color, sortDescending);

            x += widths[i];

            context.FillRect(new Rect(x - 1, header.Y, 1, header.Height), grid);
        }

        context.Restore();
    }

    /// <summary>Draws the small triangle that shows the sort direction.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="area">The space reserved for it.</param>
    /// <param name="color">The color to draw it in.</param>
    /// <param name="descending">Whether it points down.</param>
    static void DrawArrow(IDrawingContext context, Rect area, Color color, bool descending)
    {
        const int rows = 4;

        var top = area.Y + ((area.Height - rows) / 2);

        for (var i = 0; i < rows; i++)
        {
            var width = descending ? 8 - (i * 2) : 2 + (i * 2);
            var row = new Rect(area.X + ((area.Width - width) / 2), top + i, width, 1);

            context.FillRect(row, color);
        }
    }

    /// <summary>Compares cell values with a column's comparer, or by their natural order, and falls back to text for values that cannot be compared.</summary>
    /// <param name="comparer">The comparer of the column, or <see langword="null"/>.</param>
    sealed class SafeComparer(IComparer<object?>? comparer) : IComparer<object?>
    {
        /// <inheritdoc/>
        public int Compare(object? x, object? y)
        {
            if (comparer is not null)
                return comparer.Compare(x, y);

            if (x is null || y is null)
                return (x is null ? 0 : 1) - (y is null ? 0 : 1);

            if (x.GetType() == y.GetType() && x is IComparable comparable)
                return comparable.CompareTo(y);

            return string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCulture);
        }
    }
}
