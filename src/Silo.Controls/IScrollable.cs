namespace Silo;

/// <summary>A Control that scrolls its content when the mouse wheel turns over it.</summary>
/// <remarks>The <see cref="PointerRouter"/> offers the wheel to the Controls under the pointer, innermost first, until one scrolls.</remarks>
public interface IScrollable
{
    /// <summary>Scrolls the content.</summary>
    /// <param name="delta">The distance in pixels. Positive scrolls down, which moves the content up.</param>
    /// <returns><see langword="true"/> when the offset changed, so a new frame is needed.</returns>
    bool ScrollBy(double delta);
}
