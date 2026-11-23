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

        var content = ContentBox(bounds);
        var side = Math.Min(content.Width, content.Height);

        Draw(context, Glyph, new Rect(content.X + ((content.Width - side) / 2), content.Y + ((content.Height - side) / 2), side, side), GetValue(ColorProperty), GetValue(IconStrokeProperty));
    }

    /// <summary>Draws a glyph into a square. A Control that shows a glyph next to other content, such as a navigation item, uses it.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="glyph">The name of the glyph. A name that is not in <see cref="Glyphs"/> draws nothing.</param>
    /// <param name="square">The square to draw in, in the coordinates of the context.</param>
    /// <param name="color">The color of the lines.</param>
    /// <param name="stroke">How thick the lines are on the 24 by 24 grid of the glyph.</param>
    internal static void Draw(IDrawingContext context, string glyph, Rect square, Color color, double stroke)
    {
        if (square.Width <= 0 || square.Height <= 0 || !Glyphs.TryGet(glyph, out var lines))
            return;

        var scale = Math.Min(square.Width, square.Height) / Glyphs.Grid;
        var width = Math.Max(0.5, stroke * scale);

        foreach (var line in lines)
            context.StrokePolyline([.. line.Points.Select(p => (square.X + (p.X * scale), square.Y + (p.Y * scale)))], color, width, line.Closed);
    }
}
