using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A grey placeholder block that stands in for content that is still loading, with a light band that sweeps across it.
/// </summary>
/// <remarks>
/// <para>
/// A Skeleton is an ordinary Control that is as big as its <c>width</c> and <c>height</c>. Its <c>background</c> is the resting
/// color and <c>border-radius</c> shapes it: a small radius for a line of text, half the size for a circle. Put several of them
/// together to sketch the layout of what is coming, then swap them for the real content.
/// </para>
/// <para>
/// While <see cref="IsActive"/> is on, a band of <c>skeleton-highlight-color</c> crosses the block once every
/// <c>skeleton-period</c> milliseconds and the Skeleton is <see cref="Control.IsAnimating"/>, so the window draws a frame about every
/// 16 milliseconds. Turn it off to leave a still block.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var avatar = new Skeleton();
/// var line = new Skeleton();
///
/// avatar.AddClass("skeleton-circle");
/// line.AddClass("skeleton-line");
/// line.IsActive = false;
/// </code>
/// </example>
public class Skeleton : Control
{
    /// <summary>The fraction of the width that the sweeping band covers.</summary>
    public const double BandFraction = 0.5;

    /// <summary>The color at the middle of the sweeping band. Make it partly transparent to let the block show through.</summary>
    public static readonly StyledProperty<Color> HighlightColorProperty =
        StyledProperty.Register<Skeleton, Color>("skeleton-highlight-color", new(255, 255, 255, 150), Color.TryParse);

    /// <summary>The time the band takes to cross the block in milliseconds.</summary>
    public static readonly StyledProperty<double> PeriodProperty =
        StyledProperty.Register<Skeleton, double>("skeleton-period", 1500, ValueParsers.Pixels);

    /// <summary>
    /// Runs before the first Skeleton is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Skeleton()
    {
    }

    /// <summary>Gets or sets whether the band sweeps. The default is <see langword="true"/>.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc/>
    public override bool IsAnimating => IsActive;

    /// <summary>Finds where the sweeping band is at a time.</summary>
    /// <param name="time">The time since the start.</param>
    /// <param name="period">The time the band takes to cross in milliseconds.</param>
    /// <param name="width">The width of the block.</param>
    /// <returns>The left edge of the band relative to the block, from the band's width to the left of it to the block's width.</returns>
    public static double BandAt(TimeSpan time, double period, double width)
    {
        var band = width * BandFraction;
        var progress = period > 0 ? time.TotalMilliseconds / period % 1 : 0;

        return -band + ((width + band) * progress);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (!IsActive || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var band = bounds.Width * BandFraction;
        var left = bounds.X + BandAt(AnimationTime, GetValue(PeriodProperty), bounds.Width);
        var highlight = GetValue(HighlightColorProperty);
        var clear = highlight with { A = 0 };

        context.Save();
        context.ClipRoundRect(bounds, GetValue(BorderRadiusProperty));
        context.ClipRect(bounds);
        context.FillGradient(new Rect(left, bounds.Y, band / 2, bounds.Height), clear, highlight);
        context.FillGradient(new Rect(left + (band / 2), bounds.Y, band / 2, bounds.Height), highlight, clear);
        context.Restore();
    }
}
