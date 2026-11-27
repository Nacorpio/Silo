using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>Lets the user pick a range of numbers by dragging two thumbs along a track.</summary>
/// <remarks>
/// <para>
/// The part of the track between the thumbs is drawn in <c>slider-fill-color</c>. The thumbs cannot cross: <see cref="Lower"/>
/// is never above <see cref="Upper"/>. A press moves the thumb nearest to the pointer there, and the thumb follows the pointer
/// until the button is released. Until <see cref="Upper"/> is set, it follows <see cref="Maximum"/>. With <see cref="Step"/> set, the values snap to multiples of it, counted from
/// <see cref="Minimum"/>.
/// </para>
/// <para>
/// The RangeSlider takes the keyboard focus. Left and Right move the thumb that was used last by one <see cref="Step"/> (a
/// hundredth of the range when there is none), Home and End move it to its limit, and the thumb that the keys move is switched
/// with Up (upper) and Down (lower). Give it a <c>height</c> (about 24px), since it has no content to size itself from.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var price = new RangeSlider { Minimum = 0, Maximum = 500, Step = 10, Lower = 50, Upper = 200 };
///
/// price.RangeChanged += (_, _) => filter.Apply(price.Lower, price.Upper);
/// </code>
/// </example>
public class RangeSlider : Control, IPointerHandler
{
    /// <summary>The diameter of a thumb in pixels.</summary>
    const double ThumbSize = 16;

    /// <summary>The thickness of the track in pixels.</summary>
    const double TrackHeight = 4;

    /// <summary>The color of the whole track.</summary>
    public static readonly StyledProperty<Color> TrackColorProperty =
        StyledProperty.Register<RangeSlider, Color>("slider-track-color", new(229, 231, 235), Color.TryParse);

    /// <summary>The color of the part of the track between the thumbs.</summary>
    public static readonly StyledProperty<Color> FillColorProperty =
        StyledProperty.Register<RangeSlider, Color>("slider-fill-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The color of the thumbs.</summary>
    public static readonly StyledProperty<Color> ThumbColorProperty =
        StyledProperty.Register<RangeSlider, Color>("slider-thumb-color", new(255, 255, 255), Color.TryParse);


    /// <summary>The smallest value.</summary>
    double minimum;

    /// <summary>The largest value.</summary>
    double maximum = 1;

    /// <summary>The value of the lower thumb.</summary>
    double lower;

    /// <summary>The value of the upper thumb.</summary>
    double upper = 1;

    /// <summary>The distance that values snap to, or 0 for none.</summary>
    double step;

    /// <summary>Whether <see cref="Upper"/> still follows <see cref="Maximum"/> because nobody set it.</summary>
    bool upperFollowsMaximum = true;

    /// <summary>Whether the upper thumb is the one that moves with the keys and the pointer.</summary>
    bool upperActive;

    /// <summary>Whether a thumb is being dragged.</summary>
    bool dragging;


    /// <summary>
    /// Runs before the first RangeSlider is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static RangeSlider()
    {
    }

    /// <summary>Raised after <see cref="Lower"/> or <see cref="Upper"/> changed.</summary>
    public event EventHandler? RangeChanged;

    /// <summary>Gets or sets the smallest value. The default is 0.</summary>
    public double Minimum
    {
        get => minimum;
        set
        {
            minimum = value;

            Reclamp();
        }
    }

    /// <summary>Gets or sets the largest value. The default is 1.</summary>
    public double Maximum
    {
        get => maximum;
        set
        {
            maximum = value;

            Reclamp();
        }
    }

    /// <summary>Gets or sets the distance that values snap to. 0 (the default) means any value.</summary>
    public double Step
    {
        get => step;
        set => step = Math.Max(0, value);
    }

    /// <summary>Gets or sets the value of the lower thumb. It is kept between <see cref="Minimum"/> and <see cref="Upper"/>.</summary>
    public double Lower
    {
        get => lower;
        set => Set(Snap(value), upper);
    }

    /// <summary>Gets or sets the value of the upper thumb. It is kept between <see cref="Lower"/> and <see cref="Maximum"/>.</summary>
    public double Upper
    {
        get => upper;
        set
        {
            upperFollowsMaximum = false;

            Set(lower, Math.Max(Snap(value), lower));
        }
    }

    /// <summary>Gets whether the upper thumb is the one that the keys move.</summary>
    public bool IsUpperActive => upperActive;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        var value = ValueAt(e.X);

        upperActive = Math.Abs(value - upper) < Math.Abs(value - lower) || (value >= upper && lower == upper);
        dragging = true;

        return MoveActive(value) | SetPseudoState("active", true);
    }

    /// <inheritdoc/>
    public bool PointerDrag(PointerEvent e) => dragging && MoveActive(ValueAt(e.X));

    /// <inheritdoc/>
    public bool PointerUp(PointerEvent e)
    {
        dragging = false;

        return SetPseudoState("active", false);
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var (beforeLower, beforeUpper, beforeActive) = (lower, upper, upperActive);
        var delta = step > 0 ? step : (maximum - minimum) / 100;
        var current = upperActive ? upper : lower;

        switch (key)
        {
            case Key.Right:
                MoveActive(current + delta);
                break;

            case Key.Left:
                MoveActive(current - delta);
                break;

            case Key.Home:
                MoveActive(minimum);
                break;

            case Key.End:
                MoveActive(maximum);
                break;

            case Key.Up:
                upperActive = true;
                break;

            case Key.Down:
                upperActive = false;
                break;
        }

        return (beforeLower, beforeUpper, beforeActive) != (lower, upper, upperActive);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var span = Math.Max(0, bounds.Width - ThumbSize);
        var centerY = bounds.Height / 2;
        var lowerX = (ThumbSize / 2) + (span * Fraction(lower));
        var upperX = (ThumbSize / 2) + (span * Fraction(upper));
        var fill = GetValue(FillColorProperty);

        context.FillRect(new Rect(ThumbSize / 2, centerY - (TrackHeight / 2), span, TrackHeight), GetValue(TrackColorProperty), TrackHeight / 2);
        context.FillRect(new Rect(lowerX, centerY - (TrackHeight / 2), Math.Max(0, upperX - lowerX), TrackHeight), fill, TrackHeight / 2);

        foreach (var x in new[] { lowerX, upperX })
        {
            var thumb = new Rect(x - (ThumbSize / 2), centerY - (ThumbSize / 2), ThumbSize, ThumbSize);

            context.FillRect(thumb, GetValue(ThumbColorProperty), ThumbSize / 2);
            context.StrokeRect(thumb, fill, 2, ThumbSize / 2);
        }
    }

    /// <summary>Gets where a value lies in the range, from 0 to 1.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The fraction.</returns>
    double Fraction(double value) => maximum > minimum ? (value - minimum) / (maximum - minimum) : 0;

    /// <summary>Rounds a value to the step and keeps it in the range.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The snapped value.</returns>
    double Snap(double value)
    {
        var snapped = step > 0 ? minimum + (Math.Round((value - minimum) / step) * step) : value;

        return Math.Clamp(snapped, minimum, Math.Max(minimum, maximum));
    }

    /// <summary>Converts a pointer position to a value.</summary>
    /// <param name="x">The horizontal position relative to the border box.</param>
    /// <returns>The value under a thumb center at that position.</returns>
    double ValueAt(double x)
    {
        var span = Math.Max(1, Bounds.Width - ThumbSize);

        return minimum + (Math.Clamp((x - (ThumbSize / 2)) / span, 0, 1) * (maximum - minimum));
    }

    /// <summary>Moves the active thumb, without passing the other one.</summary>
    /// <param name="value">The wanted value.</param>
    /// <returns><see langword="true"/> when a value changed.</returns>
    bool MoveActive(double value)
    {
        var (beforeLower, beforeUpper) = (lower, upper);
        var snapped = Snap(value);

        if (upperActive)
        {
            upperFollowsMaximum = false;

            Set(lower, Math.Max(snapped, lower));
        }
        else
            Set(Math.Min(snapped, upper), upper);

        return (beforeLower, beforeUpper) != (lower, upper);
    }

    /// <summary>Stores the two values, ordered and in range, and raises the event when one changed.</summary>
    /// <param name="newLower">The wanted lower value.</param>
    /// <param name="newUpper">The wanted upper value.</param>
    void Set(double newLower, double newUpper)
    {
        var high = Math.Clamp(newUpper, minimum, Math.Max(minimum, maximum));
        var low = Math.Clamp(Math.Min(newLower, high), minimum, high);

        if (low == lower && high == upper)
            return;

        (lower, upper) = (low, high);

        RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Puts both values back in the range after it changed.</summary>
    void Reclamp() => Set(lower, upperFollowsMaximum ? maximum : upper);
}
