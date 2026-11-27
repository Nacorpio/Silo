using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A track with a draggable thumb that shows and sets how far a long content is scrolled.
/// </summary>
/// <remarks>
/// <para>
/// The bar knows nothing about what it scrolls. <see cref="Value"/> runs from <see cref="Minimum"/> to <see cref="Maximum"/>, the
/// range of the scroll offset, and <see cref="ViewportSize"/> is how much of the content is visible, in the same unit, which sizes the
/// thumb. Pressing the track moves by <see cref="LargeChange"/> toward the pointer, turning the mouse wheel over the bar moves by the wheel
/// distance, and dragging the thumb moves it along the track.
/// </para>
/// <para>
/// Give it a <c>width</c> (vertical) or <c>height</c> (horizontal) of about 12px, and the other side a length. The
/// <c>scrollbar-track-color</c> and <c>scrollbar-thumb-color</c> properties set its colors, and the <c>hover</c> and <c>active</c>
/// Pseudo-states let a theme change them while the pointer is over the bar or the thumb is dragged. The <see cref="PointerRouter"/>
/// drives it through <see cref="IPointerHandler"/>. Arrow buttons that repeat while held arrive with RepeatButton.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var bar = new ScrollBar { Maximum = 400, ViewportSize = 100 };
///
/// bar.ValueChanged += (_, _) => viewer.ScrollBy(bar.Value - viewer.Offset);
/// </code>
/// </example>
[PseudoStates("hover", "active")]
public class ScrollBar : Control, IPointerHandler, IScrollable
{
    /// <summary>The shortest the thumb gets, so that it can always be grabbed.</summary>
    const double MinimumThumbLength = 24;

    /// <summary>The space between the thumb and the edges of the track across it.</summary>
    const double ThumbInset = 2;

    /// <summary>The color of the track.</summary>
    public static readonly StyledProperty<Color> TrackColorProperty =
        StyledProperty.Register<ScrollBar, Color>("scrollbar-track-color", new(243, 244, 246), Color.TryParse);

    /// <summary>The color of the thumb.</summary>
    public static readonly StyledProperty<Color> ThumbColorProperty =
        StyledProperty.Register<ScrollBar, Color>("scrollbar-thumb-color", new(156, 163, 175), Color.TryParse);


    /// <summary>The smallest value.</summary>
    double minimum;

    /// <summary>The largest value.</summary>
    double maximum = 100;

    /// <summary>The current value.</summary>
    double value;

    /// <summary>How much of the content is visible.</summary>
    double viewportSize = 20;

    /// <summary>The distance of the wheel, when it is not the viewport.</summary>
    double smallChange = 48;

    /// <summary>Where the pointer is inside the thumb along the bar while it is dragged, or <see langword="null"/> when it is not.</summary>
    double? grab;


    /// <summary>
    /// Runs before the first ScrollBar is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ScrollBar()
    {
    }

    /// <summary>Raised after <see cref="Value"/> changed.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Gets or sets the direction the bar runs in. The default is <see cref="Silo.Orientation.Vertical"/>.</summary>
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    /// <summary>Gets or sets the smallest value. The default is 0.</summary>
    public double Minimum
    {
        get => minimum;
        set
        {
            minimum = value;

            Value = this.value;
        }
    }

    /// <summary>Gets or sets the largest value, which is how far the content can be scrolled. The default is 100.</summary>
    public double Maximum
    {
        get => maximum;
        set
        {
            maximum = value;

            Value = this.value;
        }
    }

    /// <summary>Gets or sets the value, the scroll offset. It is clamped to <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public double Value
    {
        get => value;
        set
        {
            var clamped = Math.Clamp(value, minimum, Math.Max(minimum, maximum));

            if (clamped == this.value)
                return;

            this.value = clamped;

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets how much of the content is visible, in the unit of <see cref="Value"/>. It sizes the thumb. The default is 20.</summary>
    public double ViewportSize
    {
        get => viewportSize;
        set => viewportSize = Math.Max(0, value);
    }

    /// <summary>Gets or sets the distance that the mouse wheel moves the bar by. The default is 48.</summary>
    public double SmallChange
    {
        get => smallChange;
        set => smallChange = Math.Max(0, value);
    }

    /// <summary>Gets or sets the distance that pressing the track moves the bar by. By default, it is <see cref="ViewportSize"/>.</summary>
    public double? LargeChange { get; set; }

    /// <summary>Gets the distance from the start of the bar to the start of the thumb, in pixels. Arrange the tree first.</summary>
    public double ThumbStart
    {
        get
        {
            var range = maximum - minimum;
            var room = Length - ThumbLength;

            return range > 0 ? (value - minimum) / range * room : 0;
        }
    }

    /// <summary>Gets the length of the thumb along the bar, in pixels. It fills the track when everything is visible. Arrange the tree first.</summary>
    public double ThumbLength
    {
        get
        {
            var range = maximum - minimum;

            if (range <= 0)
                return Length;

            var share = Length * viewportSize / (range + viewportSize);

            return Math.Clamp(share, Math.Min(MinimumThumbLength, Length), Length);
        }
    }

    /// <summary>Gets the length of the bar along its direction.</summary>
    double Length => Orientation == Orientation.Vertical ? Bounds.Height : Bounds.Width;

    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var before = value;

        Value += delta * smallChange / 48;

        return value != before;
    }

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        var position = Along(e.X, e.Y);

        SetPseudoState("active", true);

        if (position >= ThumbStart && position < ThumbStart + ThumbLength)
        {
            grab = position - ThumbStart;

            return true;
        }

        var page = LargeChange ?? viewportSize;

        Value += position < ThumbStart ? -page : page;

        return true;
    }

    /// <inheritdoc/>
    public bool PointerDrag(PointerEvent e)
    {
        if (grab is not { } offset)
            return false;

        var room = Length - ThumbLength;
        var before = value;

        if (room > 0)
            Value = minimum + Math.Clamp((Along(e.X, e.Y) - offset) / room, 0, 1) * (maximum - minimum);

        return value != before;
    }

    /// <inheritdoc/>
    public bool PointerUp(PointerEvent e)
    {
        grab = null;

        return SetPseudoState("active", false);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var vertical = Orientation == Orientation.Vertical;
        var thickness = vertical ? bounds.Width : bounds.Height;
        var radius = thickness / 2;

        var thumb = vertical
            ? new Rect(ThumbInset, ThumbStart, Math.Max(0, thickness - (ThumbInset * 2)), ThumbLength)
            : new Rect(ThumbStart, ThumbInset, ThumbLength, Math.Max(0, thickness - (ThumbInset * 2)));

        context.FillRect(bounds, GetValue(TrackColorProperty), radius);
        context.FillRect(thumb, GetValue(ThumbColorProperty), Math.Max(0, radius - ThumbInset));
    }

    /// <summary>Gets the position along the bar of a point.</summary>
    /// <param name="x">The horizontal position relative to the bar's border box.</param>
    /// <param name="y">The vertical position relative to the bar's border box.</param>
    /// <returns>The distance from the start of the bar.</returns>
    double Along(double x, double y) => Orientation == Orientation.Vertical ? y : x;
}
