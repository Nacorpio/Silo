using SkiaSharp;

namespace Silo.Rendering.Skia;

/// <summary>
/// An <see cref="IImage"/> backed by a Skia image.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Load"/> and <see cref="Decode"/> read the formats that Skia knows: PNG, JPEG, WebP, GIF and more.
/// <see cref="Create"/> draws a small picture in code, which is handy for generated icons.
/// </para>
/// <para>The image owns its pixels, so dispose it when it is no longer shown.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var logo = SkiaImage.Load("logo.png");
///
/// var dot = SkiaImage.Create(16, 16, canvas => canvas.DrawCircle(8, 8, 7, new SKPaint { Color = SKColors.Red }));
/// </code>
/// </example>
public sealed class SkiaImage : IImage, IDisposable
{
    /// <summary>Creates an image around a Skia image.</summary>
    /// <param name="image">The Skia image. This object takes ownership of it.</param>
    SkiaImage(SKImage image) => Image = image;

    /// <summary>Gets the underlying Skia image.</summary>
    public SKImage Image { get; }

    /// <inheritdoc/>
    public int Width => Image.Width;

    /// <inheritdoc/>
    public int Height => Image.Height;

    /// <summary>Reads an image from a file.</summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">The file is not an image that Skia can decode.</exception>
    public static SkiaImage Load(string path)
    {
        using var stream = File.OpenRead(path);

        return Decode(stream);
    }

    /// <summary>Reads an image from a stream.</summary>
    /// <param name="stream">The stream, positioned at the start of the encoded image.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">The data is not an image that Skia can decode.</exception>
    public static SkiaImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var data = SKData.Create(stream);

        return new SkiaImage(SKImage.FromEncodedData(data) ?? throw new InvalidDataException("The data is not an image."));
    }

    /// <summary>Draws a new image in code.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="draw">Draws on a transparent canvas of that size.</param>
    /// <returns>The image.</returns>
    public static SkiaImage Create(int width, int height, Action<SKCanvas> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            draw(canvas);
        }

        return new SkiaImage(SKImage.FromBitmap(bitmap));
    }

    /// <summary>Releases the pixels.</summary>
    public void Dispose() => Image.Dispose();
}
