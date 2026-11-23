using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A scrolling gallery of items, such as thumbnails or cards, arranged by a <see cref="GalleryLayout"/>. Only the items in or
/// near the viewport exist as Controls, so it stays fast with thousands of items.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Items"/> are any objects. Each one is turned into a Control by <see cref="ItemTemplate"/>. Without a template a
/// <see cref="GalleryItem"/> is shown as a picture with a title and subtitle, and any other object as its text, so a plain
/// thumbnail grid needs no template. The Control is wrapped in a cell that carries the hover and <c>selected</c> Pseudo-states,
/// so a stylesheet can paint them with <c>GalleryCell:selected</c>.
/// </para>
/// <para>
/// <see cref="Layout"/> chooses between <see cref="GalleryLayout.Grid"/>, <see cref="GalleryLayout.Justified"/>,
/// <see cref="GalleryLayout.Masonry"/>, <see cref="GalleryLayout.Strip"/> and your own. The styled properties
/// <c>item-width</c>, <c>item-height</c>, <c>gallery-gap</c> and <c>gallery-padding</c> size them, and <see cref="Columns"/>
/// fixes the column count. The justified and masonry layouts need each item's aspect ratio, which comes from the image of a
/// <see cref="GalleryItem"/> or from <see cref="SizeSelector"/>. Give the view a <c>height</c>, since it has no content to size from.
/// </para>
/// <para>
/// The wheel scrolls it, sideways for the strip. The arrow keys move between items, in two dimensions, Home and End jump to
/// the ends, and Enter raises <see cref="ItemActivated"/>, as does pressing an item twice quickly. Images are given as
/// <see cref="IImage"/> by the application; there is no loading or caching yet, and an item without an image shows the
/// placeholder color of its <c>thumb</c> class.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var gallery = new GalleryView { Name = "photos", Layout = GalleryLayout.Justified, SelectionMode = GallerySelectionMode.Multiple };
///
/// gallery.Items = photos.Select(photo => new GalleryItem(photo.Image, photo.Name)).ToList();
/// gallery.ItemActivated += (_, index) => Open(photos[index]);
/// </code>
/// </example>
public class GalleryView : Control, IScrollable
{
    /// <summary>The longest time between two presses on an item that count as a double press, in milliseconds.</summary>
    const long DoublePressMilliseconds = 400;


    /// <summary>The width of a cell in pixels.</summary>
    public static readonly StyledProperty<double> ItemWidthProperty =
        StyledProperty.Register<GalleryView, double>("item-width", 140, ValueParsers.Pixels);

    /// <summary>The height of a cell, or the target row height of the justified layout, in pixels.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        StyledProperty.Register<GalleryView, double>("item-height", 120, ValueParsers.Pixels);

    /// <summary>The space between items in pixels.</summary>
    public static readonly StyledProperty<double> GalleryGapProperty =
        StyledProperty.Register<GalleryView, double>("gallery-gap", 8, ValueParsers.Pixels);

    /// <summary>The space between the border and the items in pixels.</summary>
    public static readonly StyledProperty<double> GalleryPaddingProperty =
        StyledProperty.Register<GalleryView, double>("gallery-padding", 8, ValueParsers.Pixels);


    /// <summary>The items.</summary>
    IReadOnlyList<object> items = [];

    /// <summary>The layout.</summary>
    GalleryLayout layout = GalleryLayout.Grid;

    /// <summary>The indices of the selected items.</summary>
    readonly SortedSet<int> selected = [];

    /// <summary>The cells that exist, by item index.</summary>
    readonly Dictionary<int, GalleryCell> cells = [];

    /// <summary>The resolver that styles cells created after the last style pass.</summary>
    StyleResolver? resolver;

    /// <summary>The result of the last layout, or <see langword="null"/> when it is out of date.</summary>
    GalleryLayoutResult? arranged;

    /// <summary>The width, height and options the cached result was made for.</summary>
    GalleryLayoutOptions arrangedFor;

    /// <summary>The Control that makes an item's visual.</summary>
    Func<object, Control>? itemTemplate;

    /// <summary>Tells the natural size of an item.</summary>
    Func<object, Size>? sizeSelector;

    /// <summary>The fixed column count, or 0.</summary>
    int columns;

    /// <summary>The selection mode.</summary>
    GallerySelectionMode selectionMode = GallerySelectionMode.Single;

    /// <summary>The item the keyboard moves from, or -1.</summary>
    int current = -1;

    /// <summary>The item a Shift-click range starts at, or -1.</summary>
    int anchor = -1;

    /// <summary>How far the items are scrolled, in pixels.</summary>
    double offset;

    /// <summary>Whether the current item still has to be scrolled into view because there was no room to do it yet.</summary>
    bool revealPending;

    /// <summary>The item pressed last and when, for the double press.</summary>
    (int Index, long Time) lastPress = (-1, 0);


    /// <summary>
    /// Runs before the first GalleryView is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static GalleryView()
    {
    }

    /// <summary>Raised after the selection changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when the user presses an item twice quickly, or presses Enter on the current one. The argument is its index.</summary>
    public event EventHandler<int>? ItemActivated;

    /// <summary>Gets or sets the items. Setting it clears the selection and scrolls to the start.</summary>
    public IReadOnlyList<object> Items
    {
        get => items;
        set
        {
            items = value ?? [];
            offset = 0;
            current = anchor = -1;

            Invalidate();
            ReleaseAll();
            SetSelection([]);
        }
    }

    /// <summary>
    /// Gets or sets how an item becomes a Control. The default shows a <see cref="GalleryItem"/> as a picture with captions and
    /// anything else as text. Items are created when they scroll into view, so keep the template cheap.
    /// </summary>
    public Func<object, Control>? ItemTemplate
    {
        get => itemTemplate;
        set
        {
            itemTemplate = value;

            ReleaseAll();
        }
    }

    /// <summary>
    /// Gets or sets how the layout learns the natural size, and so the aspect ratio, of an item. The default is the size of the
    /// image of a <see cref="GalleryItem"/>, and 0 by 0, meaning none, for everything else.
    /// </summary>
    public Func<object, Size>? SizeSelector
    {
        get => sizeSelector;
        set
        {
            sizeSelector = value;

            Invalidate();
        }
    }

    /// <summary>Gets or sets the layout. The default is <see cref="GalleryLayout.Grid"/>.</summary>
    public GalleryLayout Layout
    {
        get => layout;
        set
        {
            layout = value ?? GalleryLayout.Grid;
            offset = 0;

            Invalidate();
        }
    }

    /// <summary>Gets or sets how many columns the grid and masonry layouts use, or 0 for as many as fit.</summary>
    public int Columns
    {
        get => columns;
        set
        {
            columns = Math.Max(0, value);

            Invalidate();
        }
    }

    /// <summary>Gets or sets how many items can be selected. The default is <see cref="GallerySelectionMode.Single"/>.</summary>
    public GallerySelectionMode SelectionMode
    {
        get => selectionMode;
        set
        {
            selectionMode = value;

            if (value == GallerySelectionMode.None || (value == GallerySelectionMode.Single && selected.Count > 1))
                SetSelection(value == GallerySelectionMode.None || selected.Count == 0 ? [] : [selected.Min]);
        }
    }

    /// <summary>Gets or sets the selected item, or -1. In multiple mode it is the one picked last. Setting it selects only that item.</summary>
    public int SelectedIndex
    {
        get => selected.Contains(current) ? current : (selected.Count == 0 ? -1 : selected.Min);
        set
        {
            if (selectionMode == GallerySelectionMode.None)
                return;

            var valid = value >= 0 && value < items.Count;

            current = anchor = valid ? value : -1;

            SetSelection(valid ? [value] : []);

            revealPending = true;
            Reveal(current);
        }
    }

    /// <summary>Gets the indices of the selected items in ascending order.</summary>
    public IReadOnlyList<int> SelectedIndices => [.. selected];

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets how far the items are scrolled, in pixels, along the layout's direction. It is between 0 and <see cref="MaxOffset"/>.</summary>
    public double Offset => Math.Clamp(offset, 0, MaxOffset);

    /// <summary>Gets the largest <see cref="Offset"/>: how much larger the items are than the viewport.</summary>
    /// <remarks>It is only meaningful after <see cref="Control.Arrange"/>.</remarks>
    public double MaxOffset
    {
        get
        {
            var area = Area;
            var extent = Arranged().Extent;

            return layout.IsHorizontal ? Math.Max(0, extent.Width - area.Width) : Math.Max(0, extent.Height - area.Height);
        }
    }

    /// <summary>Gets how many items have a Control right now.</summary>
    public int RealizedCount => cells.Count;

    /// <summary>Gets the rectangle of an item as the last layout placed it, relative to the area inside the padding.</summary>
    /// <param name="index">The item's index.</param>
    /// <returns>The rectangle.</returns>
    public Rect ItemBounds(int index) => Arranged().Rects[index];

    /// <inheritdoc/>
    protected override (double X, double Y) ChildOffset => layout.IsHorizontal ? (-Offset, 0) : (0, -Offset);

    /// <inheritdoc/>
    protected override Rect? ChildClip => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <summary>Gets the area the items are laid out in: the content box less the padding.</summary>
    Rect Area
    {
        get
        {
            var box = ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));
            var padding = Math.Max(0, GetValue(GalleryPaddingProperty));

            return new Rect(box.X + padding, box.Y + padding, Math.Max(0, box.Width - 2 * padding), Math.Max(0, box.Height - 2 * padding));
        }
    }

    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var before = Offset;

        offset = Math.Clamp(before + delta, 0, MaxOffset);

        return offset != before;
    }

    /// <summary>Handles a press on the gallery: selects, toggles or extends the selection, and activates on a double press.</summary>
    /// <param name="x">The horizontal position relative to the GalleryView's border box.</param>
    /// <param name="y">The vertical position relative to the border box.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the selection changed, so a new frame is needed.</returns>
    public bool PointerDown(double x, double y, KeyModifiers modifiers)
    {
        var index = IndexAt(x, y);

        if (index < 0)
            return false;

        var before = selected.ToArray();
        var now = Clock();
        var again = lastPress.Index == index && now - lastPress.Time <= DoublePressMilliseconds;

        lastPress = again ? (-1, 0) : (index, now);

        Pick(index, modifiers);

        if (again)
            ItemActivated?.Invoke(this, index);

        return !before.SequenceEqual(selected);
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (items.Count == 0)
            return false;

        var from = current < 0 ? (selected.Count > 0 ? selected.Min : -1) : current;
        var before = selected.ToArray();

        if (key == Key.Enter)
        {
            if (from >= 0)
                ItemActivated?.Invoke(this, from);

            return false;
        }

        var target = key switch
        {
            Key.Home => 0,
            Key.End => items.Count - 1,
            Key.Left => Math.Max(0, from - 1),
            Key.Right => Math.Min(items.Count - 1, from + 1),
            Key.Up => from < 0 ? 0 : Neighbor(from, -1),
            Key.Down => from < 0 ? 0 : Neighbor(from, 1),
            _ => -1,
        };

        if (target < 0)
            return false;

        Pick(target, modifiers.HasFlag(KeyModifiers.Shift) ? KeyModifiers.Shift : KeyModifiers.None);
        Reveal(target);

        return !before.SequenceEqual(selected);
    }

    /// <inheritdoc/>
    public override bool TextInput(string text)
    {
        if (text != " " || selectionMode != GallerySelectionMode.Multiple || current < 0)
            return false;

        Pick(current, KeyModifiers.Control);

        return true;
    }

    /// <summary>Gets or sets the source of the time in milliseconds that tells a double press from two single ones. Tests replace it.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <inheritdoc/>
    protected override void OnStyled(StyleResolver styles) => resolver = styles;

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect content)
    {
        var area = Area;
        var result = Arranged();

        if (revealPending && area.Width > 0 && area.Height > 0)
        {
            revealPending = false;

            Reveal(current);
        }

        offset = Math.Clamp(offset, 0, MaxOffset);

        var horizontal = layout.IsHorizontal;
        var viewport = horizontal ? area.Width : area.Height;
        var low = offset - viewport / 2;
        var high = offset + viewport * 1.5;

        for (var i = 0; i < result.Rects.Count; i++)
        {
            var rect = result.Rects[i];
            var start = horizontal ? rect.X : rect.Y;
            var end = horizontal ? rect.Right : rect.Bottom;

            if (end < low || start > high)
            {
                Release(i);

                continue;
            }

            var cell = Realize(i);

            PlaceChild(cell, new Rect(area.X + rect.X, area.Y + rect.Y, rect.Width, rect.Height));
        }

        return true;
    }

    /// <summary>Tells which item is at a point.</summary>
    /// <param name="x">The horizontal position relative to the border box.</param>
    /// <param name="y">The vertical position relative to the border box.</param>
    /// <returns>The item's index, or -1.</returns>
    int IndexAt(double x, double y)
    {
        var area = Area;
        var (dx, dy) = ChildOffset;
        var px = x - dx - area.X;
        var py = y - dy - area.Y;
        var rects = Arranged().Rects;

        for (var i = 0; i < rects.Count; i++)
            if (px >= rects[i].X && px < rects[i].Right && py >= rects[i].Y && py < rects[i].Bottom)
                return i;

        return -1;
    }

    /// <summary>Finds the item above or below another, preferring the one nearest in the other direction.</summary>
    /// <param name="from">The item to start at.</param>
    /// <param name="step">-1 for up, 1 for down.</param>
    /// <returns>The item's index, or <paramref name="from"/> when there is none.</returns>
    int Neighbor(int from, int step)
    {
        var rects = Arranged().Rects;
        var origin = rects[from];
        var best = from;
        var bestScore = double.MaxValue;

        for (var i = 0; i < rects.Count; i++)
        {
            var distance = ((rects[i].Y + rects[i].Height / 2) - (origin.Y + origin.Height / 2)) * step;

            if (distance < origin.Height / 2)
                continue;

            var score = (distance * 1000) + Math.Abs((rects[i].X + rects[i].Width / 2) - (origin.X + origin.Width / 2));

            if (score < bestScore)
            {
                best = i;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>Applies a pick, from the pointer or the keyboard, to the selection.</summary>
    /// <param name="index">The item.</param>
    /// <param name="modifiers">Control toggles the item and Shift selects the range from the anchor.</param>
    void Pick(int index, KeyModifiers modifiers)
    {
        current = index;

        PickCore(index, modifiers);

        foreach (var (i, cell) in cells)
            cell.Update(selected.Contains(i), i == current);
    }

    /// <summary>Changes the selection for a pick.</summary>
    /// <param name="index">The item.</param>
    /// <param name="modifiers">The modifier keys.</param>
    void PickCore(int index, KeyModifiers modifiers)
    {
        if (selectionMode == GallerySelectionMode.None)
            return;

        var multiple = selectionMode == GallerySelectionMode.Multiple;

        if (multiple && modifiers.HasFlag(KeyModifiers.Shift) && anchor >= 0)
        {
            SetSelection(Enumerable.Range(Math.Min(anchor, index), Math.Abs(index - anchor) + 1));

            return;
        }

        anchor = index;

        if (multiple && modifiers.HasFlag(KeyModifiers.Control))
        {
            var next = new SortedSet<int>(selected);

            if (!next.Remove(index))
                next.Add(index);

            SetSelection(next);

            return;
        }

        SetSelection([index]);
    }

    /// <summary>Replaces the selection and raises <see cref="SelectionChanged"/> when it differs.</summary>
    /// <param name="indices">The new selection.</param>
    void SetSelection(IEnumerable<int> indices)
    {
        var next = indices.Where(i => i >= 0 && i < items.Count).ToArray();

        if (selected.SetEquals(next))
            return;

        selected.Clear();
        selected.UnionWith(next);

        foreach (var (index, cell) in cells)
            cell.Update(selected.Contains(index), index == current);

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Scrolls just enough to show an item.</summary>
    /// <param name="index">The item's index, or -1.</param>
    void Reveal(int index)
    {
        var area = Area;

        if (index < 0 || index >= items.Count || area.Width <= 0 || area.Height <= 0)
            return;

        revealPending = false;

        var rect = Arranged().Rects[index];
        var horizontal = layout.IsHorizontal;
        var start = horizontal ? rect.X : rect.Y;
        var end = horizontal ? rect.Right : rect.Bottom;
        var viewport = horizontal ? area.Width : area.Height;

        if (start < offset)
            offset = start;
        else if (end > offset + viewport)
            offset = end - viewport;

        offset = Math.Clamp(offset, 0, MaxOffset);
    }

    /// <summary>Gets the layout result, computing it when something changed.</summary>
    /// <returns>The rectangles and the extent.</returns>
    GalleryLayoutResult Arranged()
    {
        var area = Area;

        var options = new GalleryLayoutOptions(
            area.Width,
            area.Height,
            Math.Max(1, GetValue(ItemWidthProperty)),
            Math.Max(1, GetValue(ItemHeightProperty)),
            Math.Max(0, GetValue(GalleryGapProperty)),
            columns);

        if (arranged is not null && arrangedFor == options)
            return arranged;

        var sizes = items.Select(SizeOf).ToArray();

        arrangedFor = options;

        return arranged = layout.Arrange(sizes, options);
    }

    /// <summary>Tells the natural size of an item.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The size, or 0 by 0 for none.</returns>
    Size SizeOf(object item)
    {
        if (sizeSelector is not null)
            return sizeSelector(item);

        return item is GalleryItem { Image: { } image } ? new Size(image.Width, image.Height) : default;
    }

    /// <summary>Makes the cached layout out of date.</summary>
    void Invalidate() => arranged = null;

    /// <summary>Gets the cell of an item, creating it when it has none.</summary>
    /// <param name="index">The item's index.</param>
    /// <returns>The cell.</returns>
    GalleryCell Realize(int index)
    {
        if (cells.TryGetValue(index, out var existing))
            return existing;

        var cell = new GalleryCell(itemTemplate?.Invoke(items[index]) ?? DefaultVisual(items[index]));

        cell.Update(selected.Contains(index), index == current);

        cells[index] = cell;
        Children.Add(cell);

        if (resolver is not null)
            cell.ApplyStyles(resolver);

        return cell;
    }

    /// <summary>Removes the cell of an item, if it has one.</summary>
    /// <param name="index">The item's index.</param>
    void Release(int index)
    {
        if (cells.Remove(index, out var cell))
            Children.Remove(cell);
    }

    /// <summary>Removes every cell.</summary>
    void ReleaseAll()
    {
        cells.Clear();
        Children.Clear();
    }

    /// <summary>Builds the default visual of an item.</summary>
    /// <param name="item">The item.</param>
    /// <returns>A picture with captions for a <see cref="GalleryItem"/>, otherwise its text.</returns>
    static Control DefaultVisual(object item)
    {
        if (item is not GalleryItem entry)
            return new TextBlock { Text = item?.ToString() ?? string.Empty };

        var visual = new Box();
        var thumb = new Image { Source = entry.Image };

        thumb.AddClass("thumb");
        visual.Children.Add(thumb);

        if (entry.Title.Length > 0)
            visual.Children.Add(Caption(entry.Title, "title"));

        if (!string.IsNullOrEmpty(entry.Subtitle))
            visual.Children.Add(Caption(entry.Subtitle, "subtitle"));

        visual.AddClass("item");

        return visual;
    }

    /// <summary>Makes a caption line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="name">The class.</param>
    /// <returns>The TextBlock.</returns>
    static TextBlock Caption(string text, string name)
    {
        var block = new TextBlock { Text = text };

        block.AddClass(name);

        return block;
    }
}
