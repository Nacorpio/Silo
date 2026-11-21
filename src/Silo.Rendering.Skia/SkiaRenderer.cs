using SkiaSharp;
using Silo.Styling.Values;

namespace Silo.Rendering.Skia;

/// <summary>
/// Renders display lists to Skia bitmaps. It is the headless path used by tests and screenshots.
/// </summary>
/// <example>
/// <code language="csharp">
/// using var bitmap = SkiaRenderer.Render(frame, 400, 300, new Color(255, 255, 255));
///
/// File.WriteAllBytes("frame.png", SkiaRenderer.Encode(bitmap));
/// </code>
/// </example>
public static class SkiaRenderer
{
    /// <summary>Replays a display list on a new bitmap.</summary>
    /// <param name="list">The frame to draw.</param>
    /// <param name="width">The bitmap width in pixels.</param>
    /// <param name="height">The bitmap height in pixels.</param>
    /// <param name="background">The color to clear the bitmap with first.</param>
    /// <returns>The bitmap, which the caller disposes.</returns>
    public static SKBitmap Render(DisplayList list, int width, int height, Color background)
    {
        ArgumentNullException.ThrowIfNull(list);

        var bitmap = new SKBitmap(width, height);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(background.R, background.G, background.B, background.A));
        list.Replay(new SkiaDrawingContext(canvas));

        return bitmap;
    }

    /// <summary>Encodes a bitmap as PNG.</summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <returns>The PNG file contents.</returns>
    public static byte[] Encode(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
