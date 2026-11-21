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
/// The selected row is painted with <c>selection-color</c> and <c>selection-text-color</c>. Rows are plain text for now. Rows
/// made of Controls arrive with templates in Silo.Views.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var files = new ListView { Name = "files" };
///
/// files.Items = Directory.GetFiles(path).Select(Path.GetFileName).ToList();
/// files.SelectionChanged += (_, _) => Open(files.Items[files.SelectedIndex]);
/// </code>
/// </example>
public class ListView : Control, IScrollable
{
    /// <summary>The space left of each row's text in pixels.</summary>
    const double TextInset = 8;


    /// <summary>The height of each row in pixels.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        StyledProperty.Register<ListView, double>("item-height", 28, ValueParsers.Pixels);

    /// <summary>The background of the selected row.</summary>
    public static readonly StyledProperty<Color> SelectionColorProperty =
        StyledProperty.Register<ListView, Color>("selection-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The text color of the selected row.</summary>
    public static readonly StyledProperty<Color> SelectionTextColorProperty =
        StyledProperty.Register<ListView, Color>("selection-text-color", new(255, 255, 255), Color.TryParse);


    /// <summary>The rows.</summary>
    IReadOnlyList<string> items = [];

    /// <summary>The index of the selected row, or -1.</summary>
    int selectedIndex = -1;

    /// <summary>How far the rows are scrolled up, in pixels.</summary>
    double offset;


    /// <summary>
    /// Runs before the first ListView is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ListView()
    {
    }

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
            offset = 0;

            SelectedIndex = -1;
        }
    }

    /// <summary>Gets or sets the index of the selected row, or -1 for none. Values outside the list select nothing.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var next = value >= 0 && value < items.Count ? value : -1;

            if (next == selectedIndex)
                return;

            selectedIndex = next;

            Reveal(next);

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets how far the rows are scrolled, in pixels. It is between 0 and <see cref="MaxOffset"/>.</summary>
    public double Offset => Math.Clamp(offset, 0, MaxOffset);

    /// <summary>Gets the largest <see cref="Offset"/>: how much taller the rows are than the viewport.</summary>
    /// <remarks>It is only meaningful after <see cref="Control.Arrange"/>.</remarks>
    public double MaxOffset => Math.Max(0, items.Count * RowHeight - Viewport.Height);

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the height of a row in pixels.</summary>
    double RowHeight => Math.Max(1, GetValue(ItemHeightProperty));

    /// <summary>Gets the visible area in the ListView's own coordinates.</summary>
    Rect Viewport => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var before = Offset;

        offset = Math.Clamp(before + delta, 0, MaxOffset);

        return offset != before;
    }

    /// <summary>Selects the row at a pointer position.</summary>
    /// <param name="y">The vertical position relative to the ListView's border box.</param>
    /// <returns><see langword="true"/> when the selection changed, so a new frame is needed.</returns>
    public bool PointerDown(double y)
    {
        var before = selectedIndex;

        SelectedIndex = (int)Math.Floor((y - Viewport.Y + Offset) / RowHeight);

        if (selectedIndex >= 0)
            Activated?.Invoke(this, selectedIndex);

        return before != selectedIndex;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = selectedIndex;
        var last = items.Count - 1;

        switch (key)
        {
            case Key.Up:
                SelectedIndex = Math.Max(0, selectedIndex - 1);
                break;

            case Key.Down:
                SelectedIndex = Math.Min(last, selectedIndex + 1);
                break;

            case Key.Home:
                SelectedIndex = 0;
                break;

            case Key.End:
                SelectedIndex = last;
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
        var color = GetValue(selected ? SelectionTextColorProperty : ColorProperty);

        if (selected)
            context.FillRect(new Rect(content.X, top, content.Width, RowHeight), GetValue(SelectionColorProperty));

        var line = Measurer().Measure(items[index], style);
        var y = top + Math.Max(0, RowHeight - line.Height) / 2;

        context.DrawText(items[index], content.X + TextInset, y, style, color);
    }

    /// <summary>Scrolls just enough to show a row.</summary>
    /// <param name="index">The row's index, or -1 for none.</param>
    void Reveal(int index)
    {
        var height = Viewport.Height;

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
}
