using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A scrolling grid of emoji with a title above each section. It is the part of the <see cref="EmojiPanel"/> that shows the emoji.
/// </summary>
/// <remarks>
/// <para>
/// The grid has a section for the <see cref="Recent"/> emoji, when there are some, and one for each group of the
/// <see cref="EmojiCatalog"/>. While <see cref="Query"/> is not empty, it shows the matches instead, in one list with no titles.
/// Only the rows in view are drawn. The title of the section in view stays at the top while its rows scroll under it.
/// </para>
/// <para>
/// Every cell is <c>cell-size</c> square and holds one emoji, drawn in the <c>emoji-font-family</c>. Because the cells have a fixed
/// size, the layout does not depend on how a font measures emoji. The grid is not focusable: the <see cref="EmojiPanel"/> sends it
/// the arrow keys, Page Up, Page Down, Home, End and Enter, and the pointer picks a cell.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var grid = new EmojiGrid { Query = "cat" };
///
/// grid.EmojiPicked += (_, emoji) => input.Insert(emoji);
/// </code>
/// </example>
[PseudoStates("hover")]
public class EmojiGrid : Control, IScrollable, IPointerHandler
{
    /// <summary>The font that emoji are drawn in. It lists the emoji font of each platform, as the first installed one wins.</summary>
    public static readonly StyledProperty<string> EmojiFontFamilyProperty =
        StyledProperty.Register<EmojiGrid, string>("emoji-font-family", EmojiFonts.Default, ValueParsers.Text);

    /// <summary>The width and the height of a cell.</summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        StyledProperty.Register<EmojiGrid, double>("cell-size", 32, ValueParsers.Pixels);

    /// <summary>The color of the section titles.</summary>
    public static readonly StyledProperty<Color> HeaderColorProperty =
        StyledProperty.Register<EmojiGrid, Color>("header-color", new(107, 114, 128), Color.TryParse);

    /// <summary>The color behind the cell under the pointer.</summary>
    public static readonly StyledProperty<Color> CellHoverColorProperty =
        StyledProperty.Register<EmojiGrid, Color>("cell-hover-color", new(0, 0, 0, 20), Color.TryParse);

    /// <summary>The color of the outline around the cell that the keyboard is on.</summary>
    public static readonly StyledProperty<Color> CursorColorProperty =
        StyledProperty.Register<EmojiGrid, Color>("cursor-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The width of the scroll bar.</summary>
    const double BarWidth = 10;

    /// <summary>How big an emoji is drawn, as a part of the cell size.</summary>
    const double GlyphScale = 0.62;


    /// <summary>One row of the grid: a section title or a run of cells.</summary>
    /// <param name="Section">The section that the row belongs to.</param>
    /// <param name="Top">The top of the row, from the top of all rows.</param>
    /// <param name="Height">The height of the row.</param>
    /// <param name="First">The index of the first emoji in the row, or -1 for a title.</param>
    /// <param name="Count">The number of emoji in the row.</param>
    readonly record struct Row(int Section, double Top, double Height, int First, int Count)
    {
        public bool IsTitle => First < 0;
    }

    /// <summary>A titled run of emoji.</summary>
    /// <param name="Title">The title, or an empty text for none.</param>
    /// <param name="First">The index of its first emoji in <see cref="values"/>.</param>
    /// <param name="Count">The number of emoji.</param>
    readonly record struct Section(string Title, int First, int Count);


    /// <summary>Scrolls the rows.</summary>
    readonly ScrollState scroll;

    /// <summary>The scroll bar, which sits over the right edge.</summary>
    readonly ScrollBar bar = new() { Orientation = Orientation.Vertical };

    /// <summary>The emoji that are shown, section after section, in the skin tone.</summary>
    readonly List<string> values = [];

    /// <summary>The sections of <see cref="values"/>.</summary>
    readonly List<Section> sections = [];

    /// <summary>The rows for the current width.</summary>
    readonly List<Row> rows = [];

    /// <summary>The emoji that were used lately.</summary>
    IReadOnlyList<string> recent = [];

    /// <summary>The query.</summary>
    string query = string.Empty;

    /// <summary>The skin tone.</summary>
    SkinTone skinTone;

    /// <summary>The number of columns that <see cref="rows"/> was made for, or 0 when the rows must be made again.</summary>
    int builtColumns;

    /// <summary>The height of all rows.</summary>
    double totalHeight;

    /// <summary>The emoji that the keyboard is on, or -1.</summary>
    int cursor = -1;

    /// <summary>The emoji under the pointer, or -1.</summary>
    int hovered = -1;

    /// <summary>The section that was in view at the last render.</summary>
    int currentSection;

    /// <summary>Whether the scroll bar was moved by us, and so must not scroll again.</summary>
    bool syncing;


    static EmojiGrid()
    {
    }

    /// <summary>Creates an empty grid. Set <see cref="Recent"/> or <see cref="Query"/>, or leave both to show every group.</summary>
    public EmojiGrid()
    {
        scroll = new ScrollState(() => totalHeight, () => Viewport.Height);

        bar.ValueChanged += (_, _) =>
        {
            if (!syncing)
                scroll.ScrollTo(bar.Value);
        };

        Children.Add(bar);

        Rebuild();
    }


    /// <summary>Occurs when an emoji is picked with the pointer or with Enter. The argument is the emoji, in the current skin tone.</summary>
    public event EventHandler<string>? EmojiPicked;

    /// <summary>Occurs when another section comes into view while scrolling.</summary>
    public event EventHandler? CurrentSectionChanged;


    /// <summary>Gets or sets the text to search for. While it is not empty the grid shows the matches in one list.</summary>
    public string Query
    {
        get => query;
        set
        {
            value ??= string.Empty;

            if (value == query)
                return;

            query = value;

            Rebuild();
            scroll.Reset();
        }
    }

    /// <summary>Gets or sets the skin tone that emoji with tones are shown in.</summary>
    public SkinTone SkinTone
    {
        get => skinTone;
        set
        {
            if (value == skinTone)
                return;

            skinTone = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the emoji to show in the Recent section, newest first. The section is left out while the list is empty or a <see cref="Query"/> is set.</summary>
    public IReadOnlyList<string> Recent
    {
        get => recent;
        set
        {
            recent = value ?? [];

            Rebuild();
        }
    }

    /// <summary>Gets the titles of the sections that are shown, in order. While a <see cref="Query"/> is set there are none.</summary>
    public IReadOnlyList<string> SectionTitles => [.. sections.Where(s => s.Title.Length > 0).Select(s => s.Title)];

    /// <summary>Gets the section that is at the top of the view, as an index into <see cref="SectionTitles"/>, or -1 when there are no titles.</summary>
    public int CurrentSection => query.Length > 0 ? -1 : currentSection;

    /// <summary>Gets the number of emoji that are shown.</summary>
    public int Count => values.Count;

    /// <summary>Gets the emoji that the keyboard is on, or <see langword="null"/>.</summary>
    public string? CursorEmoji => cursor >= 0 && cursor < values.Count ? values[cursor] : null;

    /// <summary>Gets how far the rows are scrolled.</summary>
    public double Offset => scroll.Offset;

    /// <summary>Gets how far the rows can be scrolled at most.</summary>
    public double MaxOffset => scroll.MaxOffset;

    /// <summary>Gets the size of a cell.</summary>
    double Cell => Math.Max(8, GetValue(CellSizeProperty));

    /// <summary>Gets the height of a section title.</summary>
    double TitleHeight => Math.Round(Cell * 0.75);

    /// <summary>Gets the area that rows are drawn in, in the grid's own coordinates.</summary>
    Rect Viewport => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <summary>Gets the number of cells in a row.</summary>
    int Columns => Math.Max(1, (int)Math.Floor((Viewport.Width - BarWidth) / Cell));


    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var changed = scroll.ScrollBy(delta);

        SyncBar();

        return changed;
    }

    /// <summary>Scrolls so that a section's title is at the top.</summary>
    /// <param name="index">The index into <see cref="SectionTitles"/>.</param>
    /// <returns><see langword="true"/> when the section exists.</returns>
    public bool ScrollToSection(int index)
    {
        EnsureRows();

        var title = rows.FindIndex(r => r.IsTitle && TitleIndex(r.Section) == index);

        if (index < 0 || title < 0)
            return false;

        scroll.ScrollTo(rows[title].Top);
        SyncBar();

        return true;
    }

    /// <summary>Puts the keyboard on the first emoji, when it is on none.</summary>
    public void ResetCursor() => cursor = values.Count == 0 ? -1 : query.Length > 0 ? 0 : -1;

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        var index = CellAt(e.X, e.Y);

        if (index < 0)
            return false;

        cursor = index;

        EmojiPicked?.Invoke(this, values[index]);

        return true;
    }

    /// <inheritdoc/>
    public bool PointerMove(PointerEvent e)
    {
        var index = CellAt(e.X, e.Y);

        if (index == hovered)
            return false;

        hovered = index;

        return true;
    }

    /// <inheritdoc/>
    public bool PointerLeave()
    {
        var changed = hovered >= 0;

        hovered = -1;

        return changed;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (values.Count == 0)
            return false;

        EnsureRows();

        var before = cursor;

        switch (key)
        {
            case Key.Left:
                cursor = Math.Max(0, cursor - 1);
                break;

            case Key.Right:
                cursor = Math.Min(values.Count - 1, cursor + 1);
                break;

            case Key.Up:
                cursor = cursor < 0 ? 0 : MoveRows(cursor, -1);
                break;

            case Key.Down:
                cursor = cursor < 0 ? 0 : MoveRows(cursor, 1);
                break;

            case Key.PageUp:
                cursor = MoveRows(Math.Max(0, cursor), -PageRows());
                break;

            case Key.PageDown:
                cursor = MoveRows(Math.Max(0, cursor), PageRows());
                break;

            case Key.Home:
                cursor = 0;
                break;

            case Key.End:
                cursor = values.Count - 1;
                break;

            case Key.Enter when cursor >= 0:
                EmojiPicked?.Invoke(this, values[cursor]);

                return true;

            default:
                return false;
        }

        Reveal(cursor);

        return cursor != before;
    }

    /// <inheritdoc/>
    protected override bool IsInFlow(Control child) => child != bar;

    /// <inheritdoc/>
    protected override bool HasOutOfFlowChildren => true;

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect area)
    {
        PlaceChild(bar, BarRect(area));

        return false;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);

        EnsureRows();
        scroll.Clamp();

        ((ILayoutNode)bar).SetLayout(BarRect(content));

        SyncBar();
        UpdateCurrentSection();

        var cell = Cell;
        var glyph = new TextStyle(GetValue(EmojiFontFamilyProperty), Math.Round(cell * GlyphScale));
        var header = CurrentTextStyle();
        var measurer = Measurer();
        var glyphHeight = measurer.LineHeight(glyph);
        var headerHeight = measurer.LineHeight(header);
        var top = scroll.Offset;

        context.Save();
        context.ClipRect(content);

        foreach (var row in rows)
        {
            if (row.Top + row.Height < top)
                continue;

            if (row.Top > top + content.Height)
                break;

            var y = content.Y + row.Top - top;

            if (row.IsTitle)
            {
                context.DrawText(sections[row.Section].Title, content.X + 4, y + Math.Max(0, (row.Height - headerHeight) / 2), header, GetValue(HeaderColorProperty));

                continue;
            }

            for (var i = 0; i < row.Count; i++)
                DrawCell(context, measurer, glyph, glyphHeight, row.First + i, content.X + (i * cell), y, cell);
        }

        DrawPinnedTitle(context, content, header, headerHeight, top);

        context.Restore();
    }

    /// <summary>Draws one cell.</summary>
    /// <param name="context">Draws.</param>
    /// <param name="measurer">Measures the emoji.</param>
    /// <param name="glyph">The emoji font.</param>
    /// <param name="glyphHeight">The line height of the emoji font.</param>
    /// <param name="index">The index of the emoji.</param>
    /// <param name="x">The left of the cell.</param>
    /// <param name="y">The top of the cell.</param>
    /// <param name="cell">The size of the cell.</param>
    void DrawCell(IDrawingContext context, ITextMeasurer measurer, TextStyle glyph, double glyphHeight, int index, double x, double y, double cell)
    {
        var area = new Rect(x + 1, y + 1, cell - 2, cell - 2);

        if (index == hovered)
            context.FillRect(area, GetValue(CellHoverColorProperty), 4);

        if (index == cursor)
            context.StrokeRect(area, GetValue(CursorColorProperty), 2, 4);

        var width = measurer.Measure(values[index], glyph).Width;

        context.DrawText(values[index], x + Math.Max(0, (cell - width) / 2), y + Math.Max(0, (cell - glyphHeight) / 2), glyph, GetValue(ColorProperty));
    }

    /// <summary>Draws the title of the section in view over the rows, so that it stays at the top.</summary>
    /// <param name="context">Draws.</param>
    /// <param name="content">The viewport.</param>
    /// <param name="header">The title font.</param>
    /// <param name="headerHeight">The line height of the title font.</param>
    /// <param name="top">The scroll offset.</param>
    void DrawPinnedTitle(IDrawingContext context, Rect content, TextStyle header, double headerHeight, double top)
    {
        if (query.Length > 0 || sections.Count == 0)
            return;

        var pinned = PinnedSection(top);
        var at = rows.FindIndex(r => r.IsTitle && r.Section == pinned);

        if (at < 0)
            return;

        var title = rows[at];

        if (title.Top >= top)
            return;

        var next = rows.FirstOrDefault(r => r.IsTitle && r.Section > title.Section);
        var push = next.Height > 0 ? Math.Min(0, next.Top - top - title.Height) : 0;
        var y = content.Y + push;

        context.FillRect(new Rect(content.X, y, content.Width, title.Height), GetValue(BackgroundProperty));
        context.DrawText(sections[title.Section].Title, content.X + 4, y + Math.Max(0, (title.Height - headerHeight) / 2), header, GetValue(HeaderColorProperty));
    }

    /// <summary>Finds the section whose title is the last one at or above the top of the view.</summary>
    /// <param name="top">The scroll offset.</param>
    /// <returns>The section.</returns>
    int PinnedSection(double top)
    {
        var found = 0;

        foreach (var row in rows)
        {
            if (row.Top > top)
                break;

            found = row.Section;
        }

        return found;
    }

    /// <summary>Remembers the section at the top and raises <see cref="CurrentSectionChanged"/> when it changed.</summary>
    void UpdateCurrentSection()
    {
        var section = Math.Max(0, TitleIndex(PinnedSection(scroll.Offset)));

        if (section == currentSection)
            return;

        currentSection = section;

        CurrentSectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets the index of a section among the sections that have a title.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The index into <see cref="SectionTitles"/>.</returns>
    int TitleIndex(int section) => sections.Take(section + 1).Count(s => s.Title.Length > 0) - 1;

    /// <summary>Finds the area of the scroll bar.</summary>
    /// <param name="area">The viewport.</param>
    /// <returns>The area, which is empty while nothing can scroll.</returns>
    Rect BarRect(Rect area)
        => scroll.MaxOffset <= 0 ? new Rect(area.Right, area.Y, 0, 0) : new Rect(area.Right - BarWidth, area.Y, BarWidth, area.Height);

    /// <summary>Copies the scroll position to the scroll bar.</summary>
    void SyncBar()
    {
        syncing = true;

        bar.Minimum = 0;
        bar.Maximum = scroll.MaxOffset;
        bar.ViewportSize = Viewport.Height;
        bar.LargeChange = Viewport.Height;
        bar.Value = scroll.Offset;

        syncing = false;
    }

    /// <summary>Finds the emoji under a position.</summary>
    /// <param name="x">The position, relative to the grid.</param>
    /// <param name="y">The position, relative to the grid.</param>
    /// <returns>The index into <see cref="values"/>, or -1.</returns>
    int CellAt(double x, double y)
    {
        EnsureRows();

        var content = Viewport;
        var column = (int)Math.Floor((x - content.X) / Cell);
        var at = y - content.Y + scroll.Offset;

        if (column < 0 || x >= content.Right - (scroll.MaxOffset > 0 ? BarWidth : 0) || at < 0)
            return -1;

        foreach (var row in rows)
            if (!row.IsTitle && at >= row.Top && at < row.Top + row.Height)
                return column < row.Count ? row.First + column : -1;

        return -1;
    }

    /// <summary>Moves the keyboard up or down by whole rows, keeping the column as far as the row reaches.</summary>
    /// <param name="index">The emoji that the keyboard is on.</param>
    /// <param name="step">How many rows, negative for up.</param>
    /// <returns>The new index. It is the same when there is no row in that direction.</returns>
    int MoveRows(int index, int step)
    {
        var cells = rows.Where(r => !r.IsTitle).ToList();
        var at = cells.FindIndex(r => index >= r.First && index < r.First + r.Count);

        if (at < 0)
            return index;

        var target = Math.Clamp(at + step, 0, cells.Count - 1);
        var column = index - cells[at].First;

        return cells[target].First + Math.Min(column, cells[target].Count - 1);
    }

    /// <summary>Gets how many rows a page holds, less one so that the last row stays in view.</summary>
    /// <returns>The number of rows, at least one.</returns>
    int PageRows() => Math.Max(1, (int)(Viewport.Height / Cell) - 1);

    /// <summary>Scrolls so that an emoji, and the title above it when it starts a section, is in view.</summary>
    /// <param name="index">The index of the emoji.</param>
    void Reveal(int index)
    {
        var at = rows.FindIndex(r => !r.IsTitle && index >= r.First && index < r.First + r.Count);

        if (at < 0)
            return;

        var row = rows[at];
        var title = at > 0 && rows[at - 1].IsTitle ? rows[at - 1] : (Row?)null;

        scroll.EnsureVisible(title?.Top ?? row.Top, row.Top + row.Height - (title?.Top ?? row.Top));

        // A title that is pinned covers the top row, so an emoji just under it has to scroll a title higher.
        if (title is null && query.Length == 0 && row.Top - scroll.Offset < TitleHeight)
            scroll.ScrollTo(row.Top - TitleHeight);

        SyncBar();
    }

    /// <summary>Collects the sections and the emoji in them, and makes the rows invalid.</summary>
    void Rebuild()
    {
        values.Clear();
        sections.Clear();

        if (query.Length > 0)
        {
            var first = values.Count;

            values.AddRange(EmojiCatalog.Find(query).Select(e => e.In(skinTone)));

            sections.Add(new Section(string.Empty, first, values.Count - first));
        }
        else
        {
            if (recent.Count > 0)
                AddSection(EmojiPanel.RecentTitle, recent);

            foreach (var group in EmojiCatalog.All.GroupBy(e => e.Group))
                AddSection(group.Key, [.. group.Select(e => e.In(skinTone))]);
        }

        cursor = query.Length > 0 && values.Count > 0 ? 0 : -1;
        hovered = -1;
        builtColumns = 0;
        currentSection = 0;
    }

    /// <summary>Adds a titled section.</summary>
    /// <param name="title">The title.</param>
    /// <param name="emoji">The emoji of the section.</param>
    void AddSection(string title, IReadOnlyList<string> emoji)
    {
        sections.Add(new Section(title, values.Count, emoji.Count));
        values.AddRange(emoji);
    }

    /// <summary>Makes the rows when the number of columns changed or the sections did.</summary>
    void EnsureRows()
    {
        var columns = Columns;

        if (columns == builtColumns)
            return;

        builtColumns = columns;

        rows.Clear();

        var top = 0.0;

        for (var s = 0; s < sections.Count; s++)
        {
            var section = sections[s];

            if (section.Title.Length > 0)
            {
                rows.Add(new Row(s, top, TitleHeight, -1, 0));

                top += TitleHeight;
            }

            for (var offset = 0; offset < section.Count; offset += columns)
            {
                rows.Add(new Row(s, top, Cell, section.First + offset, Math.Min(columns, section.Count - offset)));

                top += Cell;
            }
        }

        totalHeight = top;
    }
}
