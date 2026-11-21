using SkiaSharp;
using Silo.Layout;
using Silo.Styling.Values;

namespace Silo.Rendering.Skia;

/// <summary>
/// An <see cref="IDrawingContext"/> that draws on a SkiaSharp canvas.
/// </summary>
/// <param name="canvas">The canvas to draw on. The caller keeps ownership and disposes it.</param>
/// <remarks>
/// <para>
/// Save and restore map directly to the canvas state stack. Anti-aliasing is always on.
/// </para>
/// <para>
/// The same font resolution is used by <see cref="SkiaTextMeasurer"/>, so measured and drawn text agree.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var bitmap = new SKBitmap(200, 100);
/// using var canvas = new SKCanvas(bitmap);
///
/// var context = new SkiaDrawingContext(canvas);
///
/// context.FillRect(new Rect(10, 10, 80, 30), new Color(59, 130, 246), cornerRadius: 4);
/// </code>
/// </example>
public sealed class SkiaDrawingContext(SKCanvas canvas) : IDrawingContext
{
    /// <inheritdoc/>
    public void Save() => canvas.Save();

    /// <inheritdoc/>
    public void Restore()
    {
        if (canvas.SaveCount <= 1)
            throw new InvalidOperationException("Restore was called without a matching Save.");

        canvas.Restore();
    }

    /// <inheritdoc/>
    public void Translate(double dx, double dy) => canvas.Translate((float)dx, (float)dy);

    /// <inheritdoc/>
    public void ClipRect(Rect rect) => canvas.ClipRect(ToSkia(rect), antialias: true);

    /// <inheritdoc/>
    public void FillRect(Rect rect, Color color, double cornerRadius = 0)
    {
        using var paint = new SKPaint { Color = ToSkia(color), IsAntialias = true, Style = SKPaintStyle.Fill };

        canvas.DrawRoundRect(ToSkia(rect), (float)cornerRadius, (float)cornerRadius, paint);
    }

    /// <inheritdoc/>
    public void StrokeRect(Rect rect, Color color, double width, double cornerRadius = 0)
    {
        using var paint = new SKPaint
        {
            Color = ToSkia(color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)width,
        };

        canvas.DrawRoundRect(ToSkia(rect), (float)cornerRadius, (float)cornerRadius, paint);
    }

    /// <inheritdoc/>
    public void DrawText(string text, double x, double y, TextStyle style, Color color)
    {
        using var typeface = SkiaTextMeasurer.CreateTypeface(style);
        using var font = new SKFont(typeface, (float)style.Size);
        using var paint = new SKPaint { Color = ToSkia(color), IsAntialias = true };

        canvas.DrawText(text, (float)x, (float)(y - font.Metrics.Ascent), font, paint);
    }

    /// <summary>Converts a layout rectangle to a Skia rectangle.</summary>
    /// <param name="rect">The layout rectangle.</param>
    /// <returns>The same area as an <see cref="SKRect"/>.</returns>
    static SKRect ToSkia(Rect rect) => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    /// <summary>Converts a Silo color to a Skia color.</summary>
    /// <param name="color">The Silo color.</param>
    /// <returns>The same color as an <see cref="SKColor"/>.</returns>
    static SKColor ToSkia(Color color) => new(color.R, color.G, color.B, color.A);
}
