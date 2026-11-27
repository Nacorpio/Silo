namespace Silo;

/// <summary>
/// Implemented by a Control that handles the primary pointer button itself, such as a text editor that places the caret and
/// extends a selection while it is dragged, or a Rating that shows the value under the pointer.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="PointerRouter"/> gives hover and press to the innermost Control under the pointer that implements it. The
/// Control that got <see cref="PointerDown"/> keeps the pointer until the button is released: it gets <see cref="PointerDrag"/>
/// and <see cref="PointerUp"/> even when the pointer is outside of it.
/// </para>
/// <para>
/// Only <see cref="PointerDown"/> has to be implemented. The hover and drag members do nothing unless a Control needs them.
/// Every method returns whether the Control changed, so the router can tell the window that a new frame is needed.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// sealed class Canvas : Control, IPointerHandler
/// {
///     public bool PointerDown(PointerEvent e) => true;
///
///     public bool PointerDrag(PointerEvent e) => true;
/// }
/// </code>
/// </example>
public interface IPointerHandler
{
    /// <summary>Handles the press of the primary button.</summary>
    /// <param name="e">The pointer position relative to the Control's border box, and the modifier keys that are held.</param>
    /// <returns><see langword="true"/> when the Control changed, so a new frame is needed.</returns>
    bool PointerDown(PointerEvent e);

    /// <summary>Handles a move of the pointer while the button that <see cref="PointerDown"/> got is held.</summary>
    /// <param name="e">The pointer position relative to the Control's border box, which can be outside of it.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    bool PointerDrag(PointerEvent e) => false;

    /// <summary>Handles the release of the primary button.</summary>
    /// <param name="e">The pointer position relative to the Control's border box, which can be outside of it.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    bool PointerUp(PointerEvent e) => false;

    /// <summary>Handles a move of the pointer over the Control while no button is held by this Control.</summary>
    /// <param name="e">The pointer position relative to the Control's border box.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    bool PointerMove(PointerEvent e) => false;

    /// <summary>Handles the pointer leaving the Control, or another Control becoming the one under the pointer.</summary>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    bool PointerLeave() => false;
}
