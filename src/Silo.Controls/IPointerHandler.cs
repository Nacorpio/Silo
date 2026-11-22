namespace Silo;

/// <summary>
/// Implemented by a Control that handles the primary pointer button itself, including dragging while it is held, such as a
/// text editor that places the caret and extends a selection.
/// </summary>
/// <remarks>
/// The <see cref="PointerRouter"/> calls the nearest Control under the pointer that implements it. Positions are relative to the
/// Control's own border box.
/// </remarks>
/// <example>
/// <code language="csharp">
/// sealed class Canvas : Control, IPointerHandler
/// {
///     public bool PointerDown(double x, double y) => true;
///
///     public bool PointerDrag(double x, double y) => true;
///
///     public void PointerUp()
///     {
///     }
/// }
/// </code>
/// </example>
public interface IPointerHandler
{
    /// <summary>Handles the press of the primary button.</summary>
    /// <param name="x">The horizontal position relative to the Control's border box.</param>
    /// <param name="y">The vertical position relative to the Control's border box.</param>
    /// <returns><see langword="true"/> when the Control changed, so a new frame is needed.</returns>
    bool PointerDown(double x, double y);

    /// <summary>Handles a move of the pointer while the button that <see cref="PointerDown"/> got is held.</summary>
    /// <param name="x">The horizontal position relative to the Control's border box, which can be outside of it.</param>
    /// <param name="y">The vertical position relative to the Control's border box, which can be outside of it.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    bool PointerDrag(double x, double y);

    /// <summary>Handles the release of the primary button.</summary>
    void PointerUp();
}
