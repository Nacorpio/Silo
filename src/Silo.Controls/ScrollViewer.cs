using Silo.Layout;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Shows part of a taller child and scrolls it vertically. The child is clipped to the ScrollViewer's content box.
/// </summary>
/// <remarks>
/// <para>
/// A ScrollViewer lays its child out in a column at the child's natural height, so the child can be taller than the
/// viewport. Set the child through <see cref="Content"/>, which gives it a local <c>flex-shrink</c> of 0 so that the flex
/// engine does not squeeze it. Give the ScrollViewer a height, or a <c>flex-grow</c> inside a column, so that it has a viewport smaller than its content.
/// </para>
/// <para>
/// The platform layer scrolls it through <see cref="PointerRouter.Scroll"/> when the mouse wheel turns over it. A vertical
/// <see cref="ScrollBar"/> overlays the right edge of the viewport, so the pointer can drag the thumb or press the track. The
/// <c>scrollbar-visibility</c> property decides when it shows: <c>hover</c> (the default) while the pointer is over the
/// ScrollViewer or the bar is being dragged, <c>always</c>, or <c>never</c>. The bar is only shown when the content is taller than the
/// viewport, and <c>scrollbar-width</c> sets its width (12px in the Default theme).
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var list = new ScrollViewer { Name = "log" };
///
/// list.Content = longColumn;
///
/// list.ScrollBy(120);
/// </code>
/// </example>
public class ScrollViewer : Control, IScrollable
{
    /// <summary>When the scroll bar shows: <c>hover</c>, <c>always</c> or <c>never</c>.</summary>
    public static readonly StyledProperty<string> ScrollBarVisibilityProperty =
        StyledProperty.Register<ScrollViewer, string>("scrollbar-visibility", "hover", ValueParsers.Text);

    /// <summary>The width of the scroll bar in pixels.</summary>
    public static readonly StyledProperty<double> ScrollBarWidthProperty =
        StyledProperty.Register<ScrollViewer, double>("scrollbar-width", 12, ValueParsers.Pixels);

    /// <summary>How far the content has been scrolled up.</summary>
    readonly ScrollState scroll;

    /// <summary>The Control that is scrolled.</summary>
    Control? content;

    /// <summary>The overlay bar.</summary>
    readonly ScrollBar bar = new() { Orientation = Orientation.Vertical };

    /// <summary>Whether the bar is changing the offset or the offset is changing the bar, so the change is not echoed back.</summary>
    bool syncing;

    /// <summary>
    /// Runs before the first ScrollViewer is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ScrollViewer()
    {
    }

    /// <summary>Creates a ScrollViewer with no content.</summary>
    public ScrollViewer()
    {
        scroll = new ScrollState(() => ContentExtent, () => Viewport.Height);

        bar.ValueChanged += (_, _) =>
        {
            if (syncing)
                return;

            scroll.ScrollTo(bar.Value);
        };

        Children.Add(bar);
    }

    /// <summary>Gets the overlay scroll bar.</summary>
    public ScrollBar ScrollBar => bar;

    /// <summary>Gets whether the scroll bar is shown now: there is something to scroll and the visibility setting allows it.</summary>
    public bool IsScrollBarVisible
    {
        get
        {
            if (MaxOffset <= 0)
                return false;

            return GetValue(ScrollBarVisibilityProperty) switch
            {
                "always" => true,
                "never" => false,

                _ => PseudoStates.Contains("hover") || bar.PseudoStates.Contains("active"),
            };
        }
    }

    /// <summary>Gets or sets the Control that is scrolled. Setting it replaces the previous content.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            if (content is not null)
                Children.Remove(content);

            content = value;

            if (content is null)
                return;

            content.SetValue(FlexShrinkProperty, 0);

            Children.Insert(0, content);
        }
    }

    /// <summary>Gets the distance the content is scrolled, in pixels. It is between 0 and <see cref="MaxOffset"/>.</summary>
    public double Offset => scroll.Offset;

    /// <summary>Gets the largest <see cref="Offset"/>: how much taller the content is than the viewport. It is 0 when everything fits.</summary>
    /// <remarks>It is only meaningful after <see cref="Control.Arrange"/>.</remarks>
    public double MaxOffset => scroll.MaxOffset;

    /// <summary>Gets the height of the children, from the top of the first to the bottom of the last.</summary>
    public double ContentExtent => content is null ? 0 : content.Bounds.Bottom - Viewport.Y;

    /// <summary>Gets the visible area in the ScrollViewer's own coordinates.</summary>
    Rect Viewport => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <inheritdoc/>
    protected override (double X, double Y) ChildOffset => (0, -Offset);

    /// <inheritdoc/>
    protected override (double X, double Y) ChildOffsetFor(Control child) => child == bar ? default : ChildOffset;

    /// <inheritdoc/>
    protected override bool IsInFlow(Control child) => child != bar;

    /// <inheritdoc/>
    protected override bool HasOutOfFlowChildren => true;

    /// <inheritdoc/>
    protected override Rect? ChildClip => Viewport;

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect area)
    {
        PlaceChild(bar, BarRect(area));

        return false;
    }

    /// <summary>Finds where the bar is: along the right edge of the viewport when it is shown, and an empty rectangle when it is not.</summary>
    /// <param name="area">The viewport.</param>
    /// <returns>The bar's border box.</returns>
    Rect BarRect(Rect area)
    {
        if (!IsScrollBarVisible)
            return new Rect(area.Right, area.Y, 0, 0);

        var width = Math.Min(area.Width, Math.Max(0, GetValue(ScrollBarWidthProperty)));

        return new Rect(area.Right - width, area.Y, width, area.Height);
    }

    /// <inheritdoc/>
    protected override void OnRender(Silo.Rendering.IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        // The content has its size now, which the first pass of a layout does not know yet.
        ((ILayoutNode)bar).SetLayout(BarRect(Viewport));

        SyncBar();
    }

    /// <summary>Copies the scroll position and extent to the bar.</summary>
    void SyncBar()
    {
        syncing = true;

        bar.Minimum = 0;
        bar.Maximum = MaxOffset;
        bar.ViewportSize = Viewport.Height;
        bar.LargeChange = Viewport.Height;
        bar.Value = Offset;

        syncing = false;
    }

    /// <inheritdoc/>
    /// <remarks>A ScrollViewer always lays its child out in a column, whatever <c>flex-direction</c> says.</remarks>
    protected override FlexDirection LayoutDirection => FlexDirection.Column;

    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var changed = scroll.ScrollBy(delta);

        SyncBar();

        return changed;
    }

    /// <summary>Scrolls to the top.</summary>
    /// <returns><see langword="true"/> when the offset changed.</returns>
    public bool ScrollToTop() => ScrollBy(-Offset);
}
