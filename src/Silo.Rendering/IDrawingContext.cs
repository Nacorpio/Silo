using Silo.Layout;
using Silo.Styling.Values;

namespace Silo.Rendering;

/// <summary>
/// The drawing surface that Elements render to, shaped after a canvas such as Skia's.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates are device-independent pixels, with the origin at the top left and y growing downwards. The context keeps a
/// stack of transform and clip states: <see cref="Save"/> pushes the current state and <see cref="Restore"/> pops it.
/// </para>
/// <para>
/// A backend implements this interface to draw for real. <see cref="DisplayListBuilder"/> implements it by recording, so the
/// UI thread can produce an immutable <see cref="DisplayList"/> that the render thread replays.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// void Render(IDrawingContext context, Rect bounds)
/// {
///     context.FillRect(bounds, new Color(59, 130, 246), cornerRadius: 4);
///
///     context.Save();
///     context.Translate(bounds.X + 8, bounds.Y + 4);
///     context.DrawText("Save", 0, 0, new TextStyle("Inter", 14), new Color(255, 255, 255));
///     context.Restore();
/// }
/// </code>
/// </example>
public interface IDrawingContext
{
    /// <summary>Pushes the current transform and clip on the state stack.</summary>
    void Save();

    /// <summary>Pops the state stack, undoing every <see cref="Translate"/> and <see cref="ClipRect"/> since the matching <see cref="Save"/>.</summary>
    /// <exception cref="InvalidOperationException">There is no matching <see cref="Save"/>.</exception>
    void Restore();


    /// <summary>Moves the origin of all later drawing.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    void Translate(double dx, double dy);

    /// <summary>Limits all later drawing to the intersection of the current clip and a rectangle.</summary>
    /// <param name="rect">The rectangle, in the current coordinate system.</param>
    void ClipRect(Rect rect);


    /// <summary>Fills a rectangle, optionally with rounded corners.</summary>
    /// <param name="rect">The rectangle to fill.</param>
    /// <param name="color">The fill color.</param>
    /// <param name="cornerRadius">The radius of the corners in pixels. The default is 0 for square corners.</param>
    void FillRect(Rect rect, Color color, double cornerRadius = 0);

    /// <summary>Draws the outline of a rectangle, centered on its edge.</summary>
    /// <param name="rect">The rectangle to outline.</param>
    /// <param name="color">The stroke color.</param>
    /// <param name="width">The stroke width in pixels.</param>
    /// <param name="cornerRadius">The radius of the corners in pixels. The default is 0 for square corners.</param>
    void StrokeRect(Rect rect, Color color, double width, double cornerRadius = 0);

    /// <summary>Draws a single line of text.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The left edge of the line box.</param>
    /// <param name="y">The top edge of the line box.</param>
    /// <param name="style">The font to use.</param>
    /// <param name="color">The text color.</param>
    void DrawText(string text, double x, double y, TextStyle style, Color color);

    /// <summary>Draws an image, scaled to fill a rectangle.</summary>
    /// <param name="image">The image to draw. It must come from the same backend as the context.</param>
    /// <param name="destination">The rectangle to fill. The image is stretched to it, so pass one with the image's aspect ratio to avoid distortion.</param>
    void DrawImage(IImage image, Rect destination);
}
