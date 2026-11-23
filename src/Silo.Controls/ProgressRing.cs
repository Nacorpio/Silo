using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A circular indicator that spins while something is going on, or fills as far as the work has got.
/// </summary>
/// <remarks>
/// <para>
/// By default the ring is indeterminate: an arc grows and shrinks while it turns around the ring, which tells the user that
/// something is happening without saying how long it takes. Set <see cref="Value"/> to a number from 0 to 1 to show how far a task
/// has got instead, as an arc that starts at the top. Set <see cref="IsActive"/> to <see langword="false"/> to stop the spinning and
/// hide the arc, leaving only the track.
/// </para>
/// <para>
/// The ring is as big as its <c>width</c> and <c>height</c> (24px in the Default theme). The <c>ring-color</c>, <c>ring-track-color</c>,
/// <c>ring-thickness</c> and <c>ring-period</c> properties set its look and the time of one turn in milliseconds. While it spins it
/// is <see cref="Control.IsAnimating"/>, so the window draws a frame about every 16 milliseconds.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var spinner = new ProgressRing();
/// var download = new ProgressRing { Value = 0.4 };
///
/// spinner.IsActive = false;
/// </code>
/// </example>
public class ProgressRing : Control
{
    /// <summary>The color of the arc.</summary>
    public static readonly StyledProperty<Color> RingColorProperty =
        StyledProperty.Register<ProgressRing, Color>("ring-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The color of the full circle behind the arc.</summary>
    public static readonly StyledProperty<Color> TrackColorProperty =
        StyledProperty.Register<ProgressRing, Color>("ring-track-color", new(229, 231, 235), Color.TryParse);

    /// <summary>The thickness of the ring in pixels.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        StyledProperty.Register<ProgressRing, double>("ring-thickness", 3, ValueParsers.Pixels);

    /// <summary>The time of one turn of the indeterminate ring in milliseconds.</summary>
    public static readonly StyledProperty<double> PeriodProperty =
        StyledProperty.Register<ProgressRing, double>("ring-period", 1200, ValueParsers.Pixels);


    /// <summary>The fraction that is done, or <see langword="null"/>.</summary>
    double? value;


    /// <summary>
    /// Runs before the first ProgressRing is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ProgressRing()
    {
    }

    /// <summary>Gets or sets how far the task has got, from 0 to 1, or <see langword="null"/> (the default) for a ring that spins.</summary>
    public double? Value
    {
        get => value;
        set => this.value = value is { } fraction ? Math.Clamp(fraction, 0, 1) : null;
    }

    /// <summary>Gets or sets whether the ring is on. An off ring shows only its track. The default is <see langword="true"/>.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc/>
    public override bool IsAnimating => IsActive && value is null;

    /// <summary>Finds where the arc of an indeterminate ring is at a time.</summary>
    /// <param name="time">The time since the start.</param>
    /// <param name="period">The time of one turn in milliseconds.</param>
    /// <returns>The start of the arc in degrees clockwise from the top, and its length in degrees, which swings between 30 and 270.</returns>
    public static (double Start, double Sweep) ArcAt(TimeSpan time, double period)
    {
        var turn = period > 0 ? time.TotalMilliseconds / period : 0;
        var start = (turn * 360) % 360;
        var sweep = 150 + (120 * Math.Sin(turn * 2 * Math.PI * 0.75));

        return (start, sweep);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var thickness = Math.Max(1, GetValue(ThicknessProperty));
        var radius = (Math.Min(content.Width, content.Height) - thickness) / 2;

        if (radius <= 0)
            return;

        var center = (X: content.X + (content.Width / 2), Y: content.Y + (content.Height / 2));

        context.StrokePolyline(Arc(center, radius, 0, 360), GetValue(TrackColorProperty), thickness, closed: true);

        if (!IsActive)
            return;

        var (start, sweep) = value is { } fraction
            ? (0, fraction * 360)
            : ArcAt(AnimationTime, GetValue(PeriodProperty));

        if (sweep > 0.5)
            context.StrokePolyline(Arc(center, radius, start, sweep), GetValue(RingColorProperty), thickness);
    }

    /// <summary>Builds the points of an arc.</summary>
    /// <param name="center">The middle of the ring.</param>
    /// <param name="radius">The radius of the ring.</param>
    /// <param name="start">The start in degrees clockwise from the top.</param>
    /// <param name="sweep">The length in degrees.</param>
    /// <returns>The points, a few degrees apart.</returns>
    static List<(double X, double Y)> Arc((double X, double Y) center, double radius, double start, double sweep)
    {
        var steps = Math.Max(3, (int)Math.Ceiling(sweep / 6));
        var points = new List<(double X, double Y)>(steps + 1);

        for (var i = 0; i <= steps; i++)
        {
            var angle = (start + (sweep * i / steps) - 90) * Math.PI / 180;

            points.Add((center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle))));
        }

        return points;
    }
}
