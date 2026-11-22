using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// Shows a picture.
/// </summary>
/// <remarks>
/// <para>
/// Without a <c>width</c> and <c>height</c> an Image is as large as its picture. Given a size, it scales the picture to fit
/// inside the content box without changing its aspect ratio, and centers it, so it never looks distorted. The Image paints its
/// background and border like any other Control.
/// </para>
/// <para>
/// The picture comes from a rendering backend, for example <c>SkiaImage.Load</c> in Silo.Rendering.Skia. Arrange the tree
/// again after changing <see cref="Source"/>, since the size of the Image depends on it.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var logo = new Image { Source = SkiaImage.Load("logo.png") };
///
/// logo.AddClass("small");
/// </code>
/// </example>
public class Image : Control
{
    /// <summary>Gets or sets the picture, or <see langword="null"/> to show nothing.</summary>
    public IImage? Source { get; set; }

    /// <summary>
    /// Gets or sets whether the picture is drawn as a mask: only its shape (its alpha channel) is used, and it is filled with the
    /// Control's <c>color</c>. That makes one monochrome icon follow the theme, including <c>:hover</c> and dark variants.
    /// </summary>
    public bool IsMask { get; set; }


    /// <summary>Finds where a picture goes when it is scaled to fit inside a box and centered.</summary>
    /// <param name="image">The picture.</param>
    /// <param name="box">The box to fit it in.</param>
    /// <returns>The rectangle for <see cref="IDrawingContext.DrawImage"/>, which is empty for an empty box or picture.</returns>
    internal static Rect Fit(IImage image, Rect box)
    {
        if (image.Width <= 0 || image.Height <= 0 || box.Width <= 0 || box.Height <= 0)
            return new Rect(box.X, box.Y, 0, 0);

        var scale = Math.Min(box.Width / image.Width, box.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;

        return new Rect(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
        => Source is null ? default : new Size(Source.Width, Source.Height);

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (Source is null)
            return;

        var destination = Fit(Source, ContentBox(bounds));

        if (destination.Width <= 0)
            return;

        if (IsMask)
            context.DrawMask(Source, destination, GetValue(ColorProperty));
        else
            context.DrawImage(Source, destination);
    }
}
