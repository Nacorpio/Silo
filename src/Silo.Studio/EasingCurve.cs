using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo.Studio;

/// <summary>
/// A small plot of a <see cref="Silo.Styling.Values.Motion"/>: time runs from left to right and how much of the change is shown goes up. A
/// curve that overshoots leaves the band between the two guide lines, and a marker can follow a running animation.
/// </summary>
/// <remarks>The Styled properties are <c>curve-color</c>, <c>curve-grid-color</c>, <c>curve-marker-color</c> and <c>curve-width</c>.</remarks>
/// <example>
/// <code language="csharp">
/// var plot = new EasingCurve { Motion = new Motion(TimeSpan.FromMilliseconds(400), Easing.EaseOut) };
///
/// plot.Marker = 0.5;   // shows where the animation is half way through its time
/// </code>
/// </example>
public sealed class EasingCurve : Control
{
    /// <summary>The number of points of the curve.</summary>
    const int Samples = 96;

    /// <summary>The color of the curve.</summary>
    public static readonly StyledProperty<Color> CurveColorProperty =
        StyledProperty.Register<EasingCurve, Color>("curve-color", new Color(59, 130, 246), Color.TryParse);

    /// <summary>The color of the two guide lines at 0 and 1.</summary>
    public static readonly StyledProperty<Color> GridColorProperty =
        StyledProperty.Register<EasingCurve, Color>("curve-grid-color", new Color(150, 150, 150, 120), Color.TryParse);

    /// <summary>The color of the marker.</summary>
    public static readonly StyledProperty<Color> MarkerColorProperty =
        StyledProperty.Register<EasingCurve, Color>("curve-marker-color", new Color(236, 72, 153), Color.TryParse);

    /// <summary>The width of the curve line in pixels.</summary>
    public static readonly StyledProperty<double> CurveWidthProperty =
        StyledProperty.Register<EasingCurve, double>("curve-width", 2, TryParsePixels);

    /// <summary>The motion that is plotted.</summary>
    Motion motion = new(TimeSpan.FromMilliseconds(400), Easing.Ease);

    /// <summary>How far through its time the marker is, or <see langword="null"/> for no marker.</summary>
    double? marker;

    /// <summary>Runs before the first plot is created, so that the Styled properties are registered in time.</summary>
    static EasingCurve()
    {
    }

    /// <summary>Gets or sets the motion that is plotted. A spring is plotted until it has settled.</summary>
    public Motion Motion
    {
        get => motion;
        set => motion = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets how far through its time a running animation is, from 0 to 1, or <see langword="null"/> to hide the marker. The marker sits on
    /// the curve at that point.
    /// </summary>
    public double? Marker
    {
        get => marker;
        set => marker = value is { } fraction ? Math.Clamp(fraction, 0, 1) : null;
    }

    /// <summary>Reads a number of pixels, with or without the <c>px</c> suffix.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="false"/> when the text is not a number.</returns>
    static bool TryParsePixels(string text, out double value) => double.TryParse(
        text.EndsWith("px", StringComparison.Ordinal) ? text[..^2] : text,
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture,
        out value);

    /// <summary>Works out the points of a motion.</summary>
    /// <param name="motion">The motion.</param>
    /// <param name="count">The number of points, at least 2.</param>
    /// <returns>The points: the fraction of the time and the fraction of the change that is shown.</returns>
    public static IReadOnlyList<(double Time, double Value)> Sample(Motion motion, int count = Samples)
    {
        ArgumentNullException.ThrowIfNull(motion);

        var total = motion.DurationFor();
        var points = new List<(double, double)>(Math.Max(2, count));

        for (var i = 0; i < Math.Max(2, count); i++)
        {
            var fraction = i / (double)(Math.Max(2, count) - 1);

            points.Add((fraction, motion.Progress(total * fraction)));
        }

        return points;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var points = Sample(motion);
        var low = Math.Min(0, points.Min(point => point.Value));
        var high = Math.Max(1, points.Max(point => point.Value));
        var area = new Rect(8, 8, Math.Max(1, bounds.Width - 16), Math.Max(1, bounds.Height - 16));

        double X(double time) => area.X + (time * area.Width);
        double Y(double value) => area.Bottom - ((value - low) / (high - low) * area.Height);

        var grid = GetValue(GridColorProperty);

        context.FillRect(new Rect(area.X, Y(0), area.Width, 1), grid);
        context.FillRect(new Rect(area.X, Y(1), area.Width, 1), grid);

        context.StrokePolyline([.. points.Select(point => (X(point.Time), Y(point.Value)))], GetValue(CurveColorProperty), GetValue(CurveWidthProperty));

        if (marker is not { } at)
            return;

        var value = motion.Progress(motion.DurationFor() * at);
        var dot = new Rect(X(at) - 4, Y(value) - 4, 8, 8);

        context.FillRect(dot, GetValue(MarkerColorProperty), 4);
    }
}
