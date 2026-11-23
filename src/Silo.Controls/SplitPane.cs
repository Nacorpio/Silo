using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Lays out two or more panes in a row or a column with a divider between each two that the user drags to give one pane more
/// room and its neighbor less.
/// </summary>
/// <remarks>
/// <para>
/// Add panes with <see cref="AddPane"/>. With <see cref="Orientation.Horizontal"/> (the default) the panes sit side by side and
/// the dividers are vertical lines, with <see cref="Orientation.Vertical"/> they are stacked. A pane starts with the size that
/// was asked for, the panes without one share what is left equally, and when the SplitPane is resized the panes keep their
/// proportions. Each pane has a minimum size that a drag does not go below.
/// </para>
/// <para>
/// Pressing and dragging a divider moves it. A double press on a divider collapses the pane before it, which gives its room
/// to its neighbor and takes it out of the tree, and a second double press brings it back. After a press the divider is
/// focused: the arrow keys along the split move it by 10 pixels (50 with Shift), and Enter collapses or restores the pane before it.
/// </para>
/// <para>
/// Set <see cref="IsLocked"/> to fix the layout: the user can then no longer drag a divider, collapse a pane or move a divider with the
/// keyboard, but the dividers stay where they are and code can still change the sizes with <see cref="Move"/>, <see cref="Collapse"/> and
/// <see cref="Expand"/>.
/// </para>
/// <para>
/// The <c>divider-size</c> property sets the thickness of the dividers (6px by default), and <c>divider-color</c>,
/// <c>divider-active-color</c> (while dragged) and <c>divider-focus-color</c> set their colors. Give the SplitPane a size, or a
/// <c>flex-grow</c> in its parent, since the panes are sized by it.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var split = new SplitPane();
///
/// split.AddPane(fileTree, minSize: 120, size: 240);
/// split.AddPane(editor, minSize: 200);
///
/// split.Collapse(0);
/// </code>
/// </example>
public class SplitPane : Control, IPointerHandler
{
    /// <summary>The extra room around a divider in which a press still grabs it, in pixels.</summary>
    const double Grip = 2;

    /// <summary>The longest time between two presses that make a double press, in milliseconds.</summary>
    const long DoublePressMilliseconds = 500;

    /// <summary>The thickness of the dividers in pixels.</summary>
    public static readonly StyledProperty<double> DividerSizeProperty =
        StyledProperty.Register<SplitPane, double>("divider-size", 6, ValueParsers.Pixels);

    /// <summary>The color of the dividers.</summary>
    public static readonly StyledProperty<Color> DividerColorProperty =
        StyledProperty.Register<SplitPane, Color>("divider-color", new(209, 213, 219), Color.TryParse);

    /// <summary>The color of the divider that is dragged.</summary>
    public static readonly StyledProperty<Color> DividerActiveColorProperty =
        StyledProperty.Register<SplitPane, Color>("divider-active-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The color of the divider that has the keyboard focus.</summary>
    public static readonly StyledProperty<Color> DividerFocusColorProperty =
        StyledProperty.Register<SplitPane, Color>("divider-focus-color", new(147, 197, 253), Color.TryParse);


    /// <summary>The panes, in order, including the collapsed ones.</summary>
    readonly List<Control> panes = [];

    /// <summary>The smallest size of each pane.</summary>
    readonly List<double> minimums = [];

    /// <summary>The size that was asked for when each pane was added, or <see langword="null"/>.</summary>
    readonly List<double?> requested = [];

    /// <summary>The size of each pane along the split, in pixels. It is 0 for a collapsed pane.</summary>
    readonly List<double> sizes = [];

    /// <summary>The size that a collapsed pane had before, or 0.</summary>
    readonly List<double> saved = [];

    /// <summary>Whether each pane is collapsed.</summary>
    readonly List<bool> collapsed = [];

    /// <summary>The room that the panes shared at the last layout, so that a resize can scale them.</summary>
    double lastAvailable;

    /// <summary>The divider that is dragged, or -1.</summary>
    int dragging = -1;

    /// <summary>Where the pointer is inside the dragged divider, along the split.</summary>
    double grab;

    /// <summary>The divider that has the keyboard focus, or -1.</summary>
    int focused = -1;

    /// <summary>Whether the user is kept from changing the sizes.</summary>
    bool isLocked;

    /// <summary>The divider that was pressed last, and when, for double presses.</summary>
    (int Divider, long Time) lastPress = (-1, 0);


    /// <summary>
    /// Runs before the first SplitPane is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static SplitPane()
    {
    }

    /// <summary>Gets or sets the direction the panes are placed in. The default is <see cref="Silo.Orientation.Horizontal"/>, side by side.</summary>
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <summary>Gets or sets whether the user is kept from changing the sizes. The default is <see langword="false"/>.</summary>
    /// <remarks>A locked SplitPane ignores presses on its dividers and the arrow and Enter keys. Code can still change the sizes.</remarks>
    public bool IsLocked
    {
        get => isLocked;
        set
        {
            isLocked = value;

            if (!value)
                return;

            dragging = -1;
            focused = -1;
        }
    }

    /// <summary>Gets the panes in order, including the collapsed ones.</summary>
    public IReadOnlyList<Control> Panes => panes;

    /// <summary>Gets or sets the source of the time in milliseconds that tells a double press from two single ones. Tests replace it.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the divider that has the keyboard focus, from 0, or -1 when none has.</summary>
    public int FocusedDivider => focused;

    /// <summary>Adds a pane after the last one.</summary>
    /// <param name="pane">The Control to show.</param>
    /// <param name="minSize">The smallest size the pane can be dragged to, in pixels. The default is 48.</param>
    /// <param name="size">The size of the pane to start with, or <see langword="null"/> to share the room that is left equally with the other such panes.</param>
    public void AddPane(Control pane, double minSize = 48, double? size = null)
    {
        ArgumentNullException.ThrowIfNull(pane);

        panes.Add(pane);
        minimums.Add(Math.Max(0, minSize));
        requested.Add(size);
        saved.Add(0);
        collapsed.Add(false);

        sizes.Clear();

        Rebuild();
    }

    /// <summary>Gets the size of a pane along the split.</summary>
    /// <param name="index">The pane's index.</param>
    /// <returns>The size in pixels as of the last layout, which is 0 for a collapsed pane.</returns>
    public double SizeOf(int index) => index < sizes.Count ? sizes[index] : 0;

    /// <summary>Tells whether a pane is collapsed.</summary>
    /// <param name="index">The pane's index.</param>
    /// <returns><see langword="true"/> when the pane is out of the tree and its room belongs to a neighbor.</returns>
    public bool IsCollapsed(int index) => collapsed[index];

    /// <summary>Collapses a pane: its room goes to a neighbor and it leaves the tree.</summary>
    /// <param name="index">The pane's index.</param>
    /// <returns><see langword="true"/> when it was collapsed. It is not when it already is, or when no other pane is left to take the room.</returns>
    public bool Collapse(int index)
    {
        if (collapsed[index] || sizes.Count != panes.Count || Neighbor(index) is not { } neighbor)
            return false;

        saved[index] = sizes[index];
        sizes[neighbor] += sizes[index];
        sizes[index] = 0;
        collapsed[index] = true;

        Rebuild();

        return true;
    }

    /// <summary>Brings a collapsed pane back with the size it had.</summary>
    /// <param name="index">The pane's index.</param>
    /// <returns><see langword="true"/> when it was brought back.</returns>
    public bool Expand(int index)
    {
        if (!collapsed[index] || sizes.Count != panes.Count || Neighbor(index) is not { } neighbor)
            return false;

        var take = Math.Min(saved[index], Math.Max(0, sizes[neighbor] - minimums[neighbor]));

        sizes[neighbor] -= take;
        sizes[index] = take;
        collapsed[index] = false;

        Rebuild();

        return true;
    }

    /// <summary>Moves a divider by a distance, as far as the minimum sizes of the two panes next to it allow.</summary>
    /// <param name="divider">The divider's index, from 0, between pane <paramref name="divider"/> and the next one.</param>
    /// <param name="delta">The distance in pixels. Positive moves it right or down.</param>
    /// <returns><see langword="true"/> when it moved.</returns>
    public bool Move(int divider, double delta)
    {
        if (sizes.Count != panes.Count || divider < 0 || divider >= panes.Count - 1)
            return false;

        var before = sizes[divider];
        var total = before + sizes[divider + 1];
        var low = collapsed[divider] ? 0 : Math.Min(minimums[divider], total);
        var high = collapsed[divider + 1] ? total : Math.Max(low, total - minimums[divider + 1]);

        sizes[divider] = Math.Clamp(before + delta, low, high);
        sizes[divider + 1] = total - sizes[divider];

        return sizes[divider] != before;
    }

    /// <inheritdoc/>
    public bool PointerDown(double x, double y)
    {
        var along = Along(x, y);
        var found = isLocked ? -1 : DividerAt(along);

        if (found < 0)
            return false;

        var now = Clock();
        var doublePress = lastPress.Divider == found && now - lastPress.Time <= DoublePressMilliseconds;

        lastPress = doublePress ? (-1, 0) : (found, now);
        focused = found;

        if (doublePress)
        {
            dragging = -1;

            return collapsed[found] ? Expand(found) : Collapse(found);
        }

        dragging = found;
        grab = along - DividerStart(found);

        SetPseudoState("active", true);

        return true;
    }

    /// <inheritdoc/>
    public bool PointerDrag(double x, double y)
    {
        if (dragging < 0)
            return false;

        var wanted = Along(x, y) - grab;

        return Move(dragging, wanted - DividerStart(dragging));
    }

    /// <inheritdoc/>
    public void PointerUp()
    {
        dragging = -1;

        SetPseudoState("active", false);
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (isLocked || focused < 0 || focused >= panes.Count - 1)
            return false;

        var step = modifiers.HasFlag(KeyModifiers.Shift) ? 50 : 10;
        var horizontal = Orientation == Orientation.Horizontal;

        switch (key)
        {
            case Key.Left when horizontal:
            case Key.Up when !horizontal:
                return Move(focused, -step);

            case Key.Right when horizontal:
            case Key.Down when !horizontal:
                return Move(focused, step);

            case Key.Enter:
                return collapsed[focused] ? Expand(focused) : Collapse(focused);
        }

        return false;
    }

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect content)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var thickness = Math.Max(0, GetValue(DividerSizeProperty));
        var main = horizontal ? content.Width : content.Height;
        var available = Math.Max(0, main - (thickness * Math.Max(0, panes.Count - 1)));

        UpdateSizes(available);

        var position = horizontal ? content.X : content.Y;

        for (var i = 0; i < panes.Count; i++)
        {
            if (!collapsed[i])
            {
                var rect = horizontal
                    ? new Rect(position, content.Y, sizes[i], content.Height)
                    : new Rect(content.X, position, content.Width, sizes[i]);

                PlaceChild(panes[i], rect);
            }

            position += sizes[i] + thickness;
        }

        return true;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var thickness = Math.Max(0, GetValue(DividerSizeProperty));

        for (var i = 0; i < panes.Count - 1; i++)
        {
            var start = DividerStart(i);

            var rect = Orientation == Orientation.Horizontal
                ? new Rect(start, content.Y, thickness, content.Height)
                : new Rect(content.X, start, content.Width, thickness);

            var color = i == dragging
                ? GetValue(DividerActiveColorProperty)
                : i == focused && IsFocused ? GetValue(DividerFocusColorProperty) : GetValue(DividerColorProperty);

            context.FillRect(rect, color);
        }
    }

    /// <summary>Gets whether this Control has the keyboard focus.</summary>
    bool IsFocused => PseudoStates.Contains("focus");

    /// <summary>Puts the panes that are not collapsed in the tree.</summary>
    void Rebuild()
    {
        Children.Clear();

        for (var i = 0; i < panes.Count; i++)
            if (!collapsed[i])
                Children.Add(panes[i]);
    }

    /// <summary>Gives the panes their sizes the first time, and scales them when the room changed.</summary>
    /// <param name="available">The room the panes share, without the dividers.</param>
    void UpdateSizes(double available)
    {
        if (panes.Count == 0)
            return;

        if (sizes.Count != panes.Count)
        {
            InitializeSizes(available);
            lastAvailable = available;

            return;
        }

        if (lastAvailable > 0 && available != lastAvailable)
        {
            var factor = available / lastAvailable;

            for (var i = 0; i < sizes.Count; i++)
                sizes[i] *= factor;
        }

        lastAvailable = available;
    }

    /// <summary>Gives each pane the size that was asked for, and the rest the equal share of what is left.</summary>
    /// <param name="available">The room the panes share.</param>
    void InitializeSizes(double available)
    {
        sizes.Clear();

        var fixedTotal = requested.Where(size => size is not null).Sum(size => size!.Value);
        var flexible = requested.Count(size => size is null);
        var share = flexible > 0 ? Math.Max(0, available - fixedTotal) / flexible : 0;

        foreach (var size in requested)
            sizes.Add(size ?? share);

        if (flexible == 0 && fixedTotal > 0 && fixedTotal != available)
            for (var i = 0; i < sizes.Count; i++)
                sizes[i] *= available / fixedTotal;
    }

    /// <summary>Finds the pane that takes over the room of a collapsed pane, or gives its room back.</summary>
    /// <param name="index">The pane's index.</param>
    /// <returns>The nearest pane that is not collapsed, looking right first, or <see langword="null"/> when there is none.</returns>
    int? Neighbor(int index)
    {
        for (var i = index + 1; i < panes.Count; i++)
            if (!collapsed[i])
                return i;

        for (var i = index - 1; i >= 0; i--)
            if (!collapsed[i])
                return i;

        return null;
    }

    /// <summary>Gets the position along the split of a point.</summary>
    /// <param name="x">The horizontal position relative to the border box.</param>
    /// <param name="y">The vertical position relative to the border box.</param>
    /// <returns>The x position for side by side panes, otherwise the y position.</returns>
    double Along(double x, double y) => Orientation == Orientation.Horizontal ? x : y;

    /// <summary>Gets where a divider starts along the split, relative to the border box.</summary>
    /// <param name="divider">The divider's index.</param>
    /// <returns>The distance from the border box edge.</returns>
    double DividerStart(int divider)
    {
        var content = ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));
        var thickness = Math.Max(0, GetValue(DividerSizeProperty));
        var start = Orientation == Orientation.Horizontal ? content.X : content.Y;

        for (var i = 0; i <= divider && i < sizes.Count; i++)
            start += sizes[i] + (i < divider ? thickness : 0);

        return start;
    }

    /// <summary>Finds the divider at a position along the split.</summary>
    /// <param name="along">The position.</param>
    /// <returns>The divider's index, or -1.</returns>
    int DividerAt(double along)
    {
        var thickness = Math.Max(0, GetValue(DividerSizeProperty));

        for (var i = 0; i < panes.Count - 1 && i < sizes.Count; i++)
        {
            var start = DividerStart(i);

            if (along >= start - Grip && along < start + thickness + Grip)
                return i;
        }

        return -1;
    }
}
