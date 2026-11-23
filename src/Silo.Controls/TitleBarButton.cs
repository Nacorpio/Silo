using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>One of the minimize, maximize and close buttons of a <see cref="TitleBar"/>. It draws a glyph in the text color.</summary>
public sealed class TitleBarButton : Button
{
    /// <summary>The side of the square that the glyph is drawn in, in pixels.</summary>
    const double GlyphSize = 10;

    /// <summary>The line width of the glyph in pixels.</summary>
    const double Stroke = 1.5;


    /// <summary>
    /// Runs before the first TitleBarButton is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TitleBarButton()
    {
    }

    /// <summary>Gets or sets the name of the glyph, such as <c>close</c>.</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var square = new Rect(
            content.X + ((content.Width - GlyphSize) / 2),
            content.Y + ((content.Height - GlyphSize) / 2),
            GlyphSize,
            GlyphSize);

        Icon.Draw(context, Glyph, square, GetValue(ColorProperty), Stroke * Glyphs.Grid / GlyphSize);
    }
}
