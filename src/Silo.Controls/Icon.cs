using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A small vector symbol from the built-in set of <see cref="Glyphs"/>, drawn in the text color.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="Glyph"/> to the name of a glyph, such as <c>check</c> or <c>chevron-down</c>. The Icon is a square of
/// <c>icon-size</c> (16px by default), is drawn in the <c>color</c> of the Control, and follows it when it changes, for
/// example in a selected row. <c>icon-stroke</c> sets how thick the lines are on the 24 by 24 grid of the glyph (2 by
/// default). A name that is not one of the glyphs draws nothing.
/// </para>
/// <para>For a picture of your own, use an <see cref="Image"/> with a mask image instead.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var arrow = new Icon { Glyph = "chevron-down" };
///
/// arrow.AddClass("small");
/// </code>
/// </example>
public class Icon : Control
{
    /// <summary>The width and height of the icon in pixels.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        StyledProperty.Register<Icon, double>("icon-size", 16, ValueParsers.Pixels);

    /// <summary>How thick the lines are on the 24 by 24 grid of the glyph.</summary>
    public static readonly StyledProperty<double> IconStrokeProperty =
        StyledProperty.Register<Icon, double>("icon-stroke", 2, ValueParsers.Pixels);


    /// <summary>
    /// Runs before the first Icon is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Icon()
    {
    }

    /// <summary>Gets or sets the name of the glyph. An empty name, or one that is not in <see cref="Glyphs"/>, shows nothing.</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var size = Math.Max(0, GetValue(IconSizeProperty));

        return new Size(size, size);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (!Glyphs.TryGet(Glyph, out var lines))
            return;

        var content = ContentBox(bounds);
        var side = Math.Min(content.Width, content.Height);

        if (side <= 0)
            return;

        var scale = side / Glyphs.Grid;
        var left = content.X + ((content.Width - side) / 2);
        var top = content.Y + ((content.Height - side) / 2);
        var width = Math.Max(0.5, GetValue(IconStrokeProperty) * scale);
        var color = GetValue(ColorProperty);

        foreach (var line in lines)
            context.StrokePolyline([.. line.Points.Select(p => (left + (p.X * scale), top + (p.Y * scale)))], color, width, line.Closed);
    }
}
