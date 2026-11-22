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
/// <para>
/// Pass <c>asMask: true</c> for an icon that should take the text color of the Control that shows it. Only its transparency is
/// used then, so draw it in any color, for example black. See <see cref="IImage.IsMask"/>.
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
    /// <param name="isMask">Whether only the transparency of the image is used.</param>
    SkiaImage(SKImage image, bool isMask)
    {
        Image = image;
        IsMask = isMask;
    }

    /// <summary>Gets the underlying Skia image.</summary>
    public SKImage Image { get; }

    /// <inheritdoc/>
    public int Width => Image.Width;

    /// <inheritdoc/>
    public int Height => Image.Height;

    /// <inheritdoc/>
    public bool IsMask { get; }

    /// <summary>Reads an image from a file.</summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="asMask"><see langword="true"/> to use only the transparency of the image, so it takes the text color of its Control.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">The file is not an image that Skia can decode.</exception>
    public static SkiaImage Load(string path, bool asMask = false)
    {
        using var stream = File.OpenRead(path);

        return Decode(stream, asMask);
    }

    /// <summary>Reads an image from a stream.</summary>
    /// <param name="stream">The stream, positioned at the start of the encoded image.</param>
    /// <param name="asMask"><see langword="true"/> to use only the transparency of the image, so it takes the text color of its Control.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">The data is not an image that Skia can decode.</exception>
    public static SkiaImage Decode(Stream stream, bool asMask = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var data = SKData.Create(stream);

        var image = SKImage.FromEncodedData(data) ?? throw new InvalidDataException("The data is not an image.");

        return new SkiaImage(image, asMask);
    }

    /// <summary>Draws a new image in code.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="draw">Draws on a transparent canvas of that size.</param>
    /// <param name="asMask"><see langword="true"/> to use only the transparency of the image, so it takes the text color of its Control.</param>
    /// <returns>The image.</returns>
    public static SkiaImage Create(int width, int height, Action<SKCanvas> draw, bool asMask = false)
    {
        ArgumentNullException.ThrowIfNull(draw);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            draw(canvas);
        }

        return new SkiaImage(SKImage.FromBitmap(bitmap), asMask);
    }

    /// <summary>Releases the pixels.</summary>
    public void Dispose() => Image.Dispose();
}
