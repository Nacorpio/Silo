using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Lets the user pick a number in a range by dragging a thumb along a track.
/// </summary>
/// <remarks>
/// <para>
/// The Slider has the <c>active</c> Pseudo-state while it is dragged and <c>hover</c> while the pointer is over it. The
/// <c>slider-track-color</c>, <c>slider-fill-color</c> and <c>slider-thumb-color</c> properties set its colors. Give it a
/// <c>height</c> (about 24px), since it has no content to size itself from.
/// </para>
/// <para>The <see cref="PointerRouter"/> drives it with <see cref="PointerDown"/>, <see cref="PointerDrag"/> and <see cref="PointerUp"/>.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var volume = new Slider { Minimum = 0, Maximum = 100, Value = 40 };
///
/// volume.ValueChanged += (_, _) => player.Volume = volume.Value;
/// </code>
/// </example>
public class Slider : Control, IPointerHandler
{
    /// <summary>The diameter of the thumb in pixels.</summary>
    const double ThumbSize = 16;

    /// <summary>The thickness of the track in pixels.</summary>
    const double TrackHeight = 4;

    /// <summary>The color of the whole track.</summary>
    public static readonly StyledProperty<Color> TrackColorProperty =
        StyledProperty.Register<Slider, Color>("slider-track-color", new(229, 231, 235), Color.TryParse);

    /// <summary>The color of the part of the track before the thumb.</summary>
    public static readonly StyledProperty<Color> FillColorProperty =
        StyledProperty.Register<Slider, Color>("slider-fill-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The color of the thumb.</summary>
    public static readonly StyledProperty<Color> ThumbColorProperty =
        StyledProperty.Register<Slider, Color>("slider-thumb-color", new(255, 255, 255), Color.TryParse);


    /// <summary>The smallest value.</summary>
    double minimum;

    /// <summary>The largest value.</summary>
    double maximum = 1;

    /// <summary>The current value.</summary>
    double value;

    /// <summary>Whether the thumb is being dragged.</summary>
    bool dragging;


    /// <summary>
    /// Runs before the first Slider is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Slider()
    {
    }

    /// <summary>Raised after <see cref="Value"/> changed.</summary>
    public event EventHandler? ValueChanged;

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

    /// <summary>Gets or sets the largest value. The default is 1.</summary>
    public double Maximum
    {
        get => maximum;
        set
        {
            maximum = value;

            Value = this.value;
        }
    }

    /// <summary>Gets or sets the value. It is clamped to <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
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

    /// <summary>Starts a drag and moves the thumb to the pointer.</summary>
    /// <param name="e">The pointer position relative to the Slider's border box.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public bool PointerDown(PointerEvent e)
    {
        dragging = true;
        Value = ValueAt(e.X);

        return SetPseudoState("active", true);
    }

    /// <summary>Moves the thumb to the pointer while a drag is going on.</summary>
    /// <param name="e">The pointer position relative to the Slider's border box, which can be outside of it.</param>
    /// <returns><see langword="true"/> when the value changed, so a new frame is needed.</returns>
    public bool PointerDrag(PointerEvent e)
    {
        if (!dragging)
            return false;

        var before = value;

        Value = ValueAt(e.X);

        return before != value;
    }

    /// <summary>Ends the drag.</summary>
    /// <param name="e">The pointer position relative to the Slider's border box, which can be outside of it.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public bool PointerUp(PointerEvent e)
    {
        dragging = false;

        return SetPseudoState("active", false);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var span = Math.Max(0, bounds.Width - ThumbSize);
        var thumbX = ThumbSize / 2 + span * Fraction;
        var centerY = bounds.Height / 2;

        var track = new Rect(ThumbSize / 2, centerY - TrackHeight / 2, span, TrackHeight);
        var fill = new Rect(track.X, track.Y, span * Fraction, TrackHeight);
        var thumb = new Rect(thumbX - ThumbSize / 2, centerY - ThumbSize / 2, ThumbSize, ThumbSize);

        context.FillRect(track, GetValue(TrackColorProperty), TrackHeight / 2);
        context.FillRect(fill, GetValue(FillColorProperty), TrackHeight / 2);
        context.FillRect(thumb, GetValue(ThumbColorProperty), ThumbSize / 2);
        context.StrokeRect(thumb, GetValue(FillColorProperty), 2, ThumbSize / 2);
    }

    /// <summary>Gets where the value lies in the range, from 0 to 1.</summary>
    double Fraction => maximum > minimum ? (value - minimum) / (maximum - minimum) : 0;

    /// <summary>Converts a pointer position to a value.</summary>
    /// <param name="x">The horizontal position relative to the Slider's border box.</param>
    /// <returns>The value under the thumb center when it is at that position.</returns>
    double ValueAt(double x)
    {
        var span = Math.Max(1, Bounds.Width - ThumbSize);
        var fraction = Math.Clamp((x - ThumbSize / 2) / span, 0, 1);

        return minimum + fraction * (maximum - minimum);
    }
}
