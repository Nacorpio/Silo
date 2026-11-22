namespace Silo.Rendering;

/// <summary>
/// A decoded picture that can be drawn with <see cref="IDrawingContext.DrawImage"/>.
/// </summary>
/// <remarks>
/// <para>
/// An image is created by a backend, such as <c>SkiaImage</c> in Silo.Rendering.Skia, and only that backend can draw it.
/// Silo only needs its natural size, to lay Controls out around it. An image is immutable, so one instance can be shared by
/// any number of Controls and threads.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// IImage logo = SkiaImage.Load("logo.png");
///
/// var icon = new Image { Source = logo };
/// </code>
/// </example>
public interface IImage
{
    /// <summary>Gets the natural width in pixels.</summary>
    int Width { get; }

    /// <summary>Gets the natural height in pixels.</summary>
    int Height { get; }
}
