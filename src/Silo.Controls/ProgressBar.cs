using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Shows how far something has come: a track with a bar that fills from the left.
/// </summary>
/// <remarks>
/// The track is the Control's background and border, so style it like any Control. The bar uses the <c>bar-color</c>
/// property. Give the ProgressBar a <c>height</c>, since it has no content to size itself from.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var progress = new ProgressBar { Value = 0.4 };
///
/// progress.Value = 0.75;
/// </code>
/// </example>
public class ProgressBar : Control
{
    /// <summary>The color of the filled part.</summary>
    public static readonly StyledProperty<Color> BarColorProperty = StyledProperty.Register<ProgressBar, Color>("bar-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The progress, from 0 to 1.</summary>
    double value;

    /// <summary>
    /// Runs before the first ProgressBar is created, so that <see cref="BarColorProperty"/> is registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ProgressBar()
    {
    }

    /// <summary>Gets or sets the progress, from 0 (empty) to 1 (full). Values outside that range are clamped.</summary>
    public double Value
    {
        get => value;
        set => this.value = Math.Clamp(value, 0, 1);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var width = content.Width * value;

        if (width <= 0)
            return;

        context.FillRect(new Rect(content.X, content.Y, width, content.Height), GetValue(BarColorProperty), Math.Min(GetValue(BorderRadiusProperty), width / 2));
    }
}
