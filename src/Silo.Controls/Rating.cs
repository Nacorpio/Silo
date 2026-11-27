using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>A row of stars that the user clicks to give a rating.</summary>
/// <remarks>
/// <para>
/// The stars are the <c>star</c> glyph of <see cref="Glyphs"/>. Those up to the value are filled with <c>rating-color</c> and
/// the others are outlined with <c>rating-empty-color</c>. Each star is a square of <c>icon-size</c> (20px by default) with
/// <c>rating-gap</c> (4px) between them. While the pointer is over a Rating that is not read-only, the stars up to the
/// pointer are shown as if they were the value.
/// </para>
/// <para>
/// The Rating takes the keyboard focus. Left and Right (or Down and Up) change the value by one star, or by half a star when
/// <see cref="AllowHalf"/> is set; Home clears it when <see cref="AllowClear"/> is set; End sets the maximum.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var stars = new Rating { Maximum = 5, AllowHalf = true, Value = 3.5 };
///
/// stars.ValueChanged += (_, _) => review.Score = stars.Value;
/// </code>
/// </example>
[PseudoStates("hover", "focus")]
public class Rating : Control, IPointerHandler
{
    /// <summary>The side of each star in pixels.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        StyledProperty.Register<Rating, double>("icon-size", 20, ValueParsers.Pixels);

    /// <summary>The space between the stars in pixels.</summary>
    public static readonly StyledProperty<double> StarGapProperty =
        StyledProperty.Register<Rating, double>("rating-gap", 4, ValueParsers.Pixels);

    /// <summary>The fill of the stars up to the value.</summary>
    public static readonly StyledProperty<Color> RatingColorProperty =
        StyledProperty.Register<Rating, Color>("rating-color", new(245, 158, 11), Color.TryParse);

    /// <summary>The outline of the stars after the value.</summary>
    public static readonly StyledProperty<Color> EmptyColorProperty =
        StyledProperty.Register<Rating, Color>("rating-empty-color", new(156, 163, 175), Color.TryParse);


    /// <summary>The number of stars.</summary>
    int maximum = 5;

    /// <summary>The rating.</summary>
    double value;

    /// <summary>The value under the pointer, or <see langword="null"/> when it is not over the Rating.</summary>
    double? preview;


    /// <summary>
    /// Runs before the first Rating is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Rating()
    {
    }

    /// <summary>Raised after <see cref="Value"/> changed.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Gets or sets the number of stars. The default is 5.</summary>
    public int Maximum
    {
        get => maximum;
        set
        {
            maximum = Math.Max(1, value);

            Value = this.value;
        }
    }

    /// <summary>Gets or sets the rating, from 0 to <see cref="Maximum"/>. Without <see cref="AllowHalf"/> it is rounded to a whole star.</summary>
    public double Value
    {
        get => value;
        set
        {
            var next = Snap(Math.Clamp(value, 0, maximum));

            if (next == this.value)
                return;

            this.value = next;

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets whether the rating can be a half star. The default is <see langword="false"/>.</summary>
    public bool AllowHalf { get; set; }

    /// <summary>Gets or sets whether clicking the star of the current value sets the rating back to 0. The default is <see langword="false"/>.</summary>
    public bool AllowClear { get; set; }

    /// <summary>Gets or sets whether the user can not change the rating. The default is <see langword="false"/>.</summary>
    public bool IsReadOnly { get; set; }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the value that is drawn: the one under the pointer, if any, otherwise <see cref="Value"/>.</summary>
    public double DisplayValue => preview ?? value;

    /// <summary>Shows the value under the pointer. The pointer router calls it when the pointer moves over the Rating.</summary>
    /// <param name="e">The pointer position relative to the Rating's border box.</param>
    /// <returns><see langword="true"/> when the shown value changed, so a new frame is needed.</returns>
    public bool PointerMove(PointerEvent e)
    {
        var before = preview;

        preview = IsReadOnly ? null : ValueAt(e.X);

        return before != preview;
    }

    /// <summary>Stops showing the value under the pointer. The pointer router calls it when the pointer leaves the Rating.</summary>
    /// <returns><see langword="true"/> when the shown value changed, so a new frame is needed.</returns>
    public bool PointerLeave()
    {
        var changed = preview is not null;

        preview = null;

        return changed;
    }

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        if (IsReadOnly)
            return false;

        var before = value;
        var picked = ValueAt(e.X);

        Value = AllowClear && picked == value ? 0 : picked;

        return before != value;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        if (IsReadOnly)
            return false;

        var before = value;
        var step = AllowHalf ? 0.5 : 1;

        switch (key)
        {
            case Key.Right or Key.Up:
                Value += step;
                break;

            case Key.Left or Key.Down:
                Value -= step;
                break;

            case Key.Home when AllowClear:
                Value = 0;
                break;

            case Key.End:
                Value = maximum;
                break;
        }

        return before != value;
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var size = Math.Max(0, GetValue(IconSizeProperty));

        return new Size((size * maximum) + (Math.Max(0, GetValue(StarGapProperty)) * (maximum - 1)), size);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (!Glyphs.TryGet("star", out var lines))
            return;

        var content = ContentBox(bounds);
        var size = Math.Max(0, GetValue(IconSizeProperty));
        var gap = Math.Max(0, GetValue(StarGapProperty));
        var scale = size / Glyphs.Grid;
        var filled = GetValue(RatingColorProperty);
        var empty = GetValue(EmptyColorProperty);
        var shown = DisplayValue;

        for (var i = 0; i < maximum; i++)
        {
            var left = content.X + (i * (size + gap));
            var top = content.Y + Math.Max(0, (content.Height - size) / 2);
            var fraction = Math.Clamp(shown - i, 0, 1);

            foreach (var line in lines)
            {
                var points = line.Points.Select(p => (left + (p.X * scale), top + (p.Y * scale))).ToList();

                context.StrokePolyline(points, fraction >= 1 ? filled : empty, 1.5, closed: true);

                if (fraction <= 0)
                    continue;

                context.Save();
                context.ClipRect(new Rect(left, top, size * fraction, size));
                context.FillPolygon(points, filled);
                context.Restore();
            }
        }
    }

    /// <summary>Rounds a value to the step.</summary>
    /// <param name="raw">The value.</param>
    /// <returns>The value rounded to a half star or a whole star.</returns>
    double Snap(double raw) => AllowHalf ? Math.Round(raw * 2) / 2 : Math.Round(raw);

    /// <summary>Converts a pointer position to the rating of the star under it.</summary>
    /// <param name="x">The horizontal position relative to the Rating's border box.</param>
    /// <returns>The value that clicking at the position gives, at least a half or whole star.</returns>
    double ValueAt(double x)
    {
        var size = Math.Max(1, GetValue(IconSizeProperty));
        var pitch = size + Math.Max(0, GetValue(StarGapProperty));
        var relative = x - ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height)).X;
        var star = Math.Clamp((int)Math.Floor(relative / pitch), 0, maximum - 1);
        var inside = Math.Clamp((relative - (star * pitch)) / size, 0, 1);

        return star + (AllowHalf && inside <= 0.5 ? 0.5 : 1);
    }
}
