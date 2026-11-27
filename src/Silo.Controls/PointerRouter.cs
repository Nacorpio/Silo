namespace Silo;

/// <summary>
/// Turns pointer positions and button presses from a platform window into the Pseudo-states and clicks of a Control tree.
/// </summary>
/// <remarks>
/// <para>
/// The router keeps <c>hover</c> set on the Control under the pointer and on its ancestors, the way CSS does, and presses
/// and releases a <see cref="Button"/> that the pointer goes down on. A Button that was pressed gets the release even when
/// the pointer has left it, and it only counts as a click when the pointer is still over it.
/// </para>
/// <para>
/// Every method returns whether a Pseudo-state changed. When it does, apply the Stylesheets again, arrange if needed, and
/// render a new frame.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var router = new PointerRouter(root);
///
/// if (router.Move(120, 40))
///     root.ApplyStyles(resolver);
/// </code>
/// </example>
/// <remarks>
/// Given an <see cref="OverlayLayer"/>, the router tests the overlays before the tree. A press outside the top overlay closes
/// it and goes no further, and the wheel does not scroll what is behind an overlay.
/// </remarks>
/// <param name="root">The root Control, whose origin is the pointer's origin.</param>
/// <param name="overlays">The overlays that are drawn above the root, or <see langword="null"/> for none.</param>
public sealed class PointerRouter(Control root, OverlayLayer? overlays = null)
{
    /// <summary>The Control that the pointer is over, or <see langword="null"/> when it is outside the tree.</summary>
    Control? hovered;

    /// <summary>The Button that the pointer went down on and has not come up from yet.</summary>
    Button? pressed;

    /// <summary>The pointer handler that the pointer is over, so that it can be told when the pointer leaves.</summary>
    Control? hoveredHandler;

    /// <summary>The pointer handler that the button was pressed on, while the button is held.</summary>
    Control? capture;

    /// <summary>Gets or sets the modifier keys that are held. The platform sets it before a press, so that Ctrl-click and Shift-click work.</summary>
    public KeyModifiers Modifiers { get; set; }

    /// <summary>Gets the Control that the pointer is currently over.</summary>
    public Control? Hovered => hovered;

    /// <summary>Moves the pointer.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Move(double x, double y)
    {
        var moved = Drag(x, y);

        var hit = Hit(x, y);
        var changed = SetHovered(overlays?.IsBackdrop(hit) == true ? null : hit) | moved;

        if (overlays?.IsBackdrop(hit) == true && TitleBehindBackdrop(x, y) is { Bar: { IsOpen: true } bar } title)
            changed |= bar.Open(title);

        return MoveHandler(x, y) | changed;
    }

    /// <summary>Tells the router that the pointer left the window.</summary>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Leave()
    {
        var changed = LeaveHandler();

        hoveredHandler = null;

        return SetHovered(null) | changed;
    }

    /// <summary>Presses the primary pointer button.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Down(double x, double y)
    {
        var changed = Move(x, y);

        if (overlays?.IsBackdrop(Hit(x, y)) == true)
        {
            var title = TitleBehindBackdrop(x, y);

            if (title is { Bar: { IsOpen: true } bar })
                return bar.Toggle(title) | changed;

            return overlays.PressOutside() | changed;
        }

        pressed = Find<Button>(hovered);
        capture = FindHandler(hovered);

        if (capture is not null)
            changed |= Handler(capture).PointerDown(Local(capture, x, y));

        return (pressed?.PointerDown() ?? false) | changed;
    }

    /// <summary>Presses the secondary pointer button, which opens the <see cref="Control.ContextMenu"/> under the pointer.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a menu opened or an overlay closed, so a new frame is needed.</returns>
    /// <remarks>Like a primary press, a press outside the top overlay closes it and goes no further.</remarks>
    public bool SecondaryDown(double x, double y)
    {
        var changed = Move(x, y);

        if (overlays?.IsBackdrop(Hit(x, y)) == true)
            return overlays.PressOutside() | changed;

        for (var c = hovered; c is not null; c = c.Parent as Control)
            if (c.ContextMenu is { } menu)
                return menu.Show(c, x, y) | changed;

        return changed;
    }

    /// <summary>Releases the primary pointer button.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed. A <see cref="Button.Click"/> handler has run by then.</returns>
    public bool Up(double x, double y)
    {
        var changed = Move(x, y);

        var button = pressed;
        var held = capture;

        pressed = null;
        capture = null;

        if (held is not null)
            changed |= Handler(held).PointerUp(Local(held, x, y));

        return (button?.PointerUp(Find<Button>(hovered) == button) ?? false) | changed;
    }

    /// <summary>Turns the mouse wheel over a point.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <param name="delta">The distance to scroll in pixels. Positive scrolls down.</param>
    /// <returns><see langword="true"/> when a <see cref="IScrollable"/> scrolled, so a new frame is needed.</returns>
    public bool Scroll(double x, double y, double delta)
    {
        for (var c = Hit(x, y); c is not null; c = c.Parent as Control)
            if (c is IScrollable scrollable && scrollable.ScrollBy(delta))
                return true;

        return false;
    }

    /// <summary>Finds the Control at a point, looking at the overlays first.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns>The Control, the overlays' backdrop, or <see langword="null"/>.</returns>
    Control? Hit(double x, double y) => overlays?.HitTest(x, y) ?? root.HitTest(x, y);

    /// <summary>Finds the title of a <see cref="MenuBar"/> under a point that an open menu's backdrop covers.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns>The title, or <see langword="null"/> when the point is not on one.</returns>
    MenuBarItem? TitleBehindBackdrop(double x, double y) => Find<MenuBarItem>(root.HitTest(x, y));

    /// <summary>Moves <c>hover</c> to a Control and its ancestors.</summary>
    /// <param name="target">The new Control under the pointer, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when any Control changed.</returns>
    bool SetHovered(Control? target)
    {
        if (target == hovered)
            return false;

        var changed = false;

        for (var c = hovered; c is not null; c = c.Parent as Control)
            changed |= c.SetHovered(false);

        for (var c = target; c is not null; c = c.Parent as Control)
            changed |= c.SetHovered(true);

        hovered = target;

        return changed;
    }

    /// <summary>Gives a drag to the handler that holds the pointer.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when the handler changed.</returns>
    bool Drag(double x, double y) => capture is not null && Handler(capture).PointerDrag(Local(capture, x, y));

    /// <summary>Tells the handler under the pointer about the move, and the one that was under it before that it was left.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a handler changed.</returns>
    bool MoveHandler(double x, double y)
    {
        var handler = FindHandler(hovered);
        var changed = false;

        if (handler != hoveredHandler)
            changed |= LeaveHandler();

        hoveredHandler = handler;

        if (handler is null)
            return changed;

        return Handler(handler).PointerMove(Local(handler, x, y)) | changed;
    }

    /// <summary>Tells the handler that was under the pointer that the pointer left it.</summary>
    /// <returns><see langword="true"/> when the handler changed.</returns>
    bool LeaveHandler() => hoveredHandler is not null && Handler(hoveredHandler).PointerLeave();

    /// <summary>Makes the event that a handler gets for a pointer position.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns>The position relative to the handler, with the modifier keys that are held.</returns>
    PointerEvent Local(Control handler, double x, double y)
    {
        var (left, top) = handler.ToLocal(x, y);

        return new PointerEvent(left, top, Modifiers);
    }

    /// <summary>Finds the innermost Control that handles the pointer itself.</summary>
    /// <param name="control">The Control to start at, which is searched first, and then its ancestors.</param>
    /// <returns>The Control, or <see langword="null"/> when there is none.</returns>
    static Control? FindHandler(Control? control)
    {
        for (var c = control; c is not null; c = c.Parent as Control)
            if (c is IPointerHandler)
                return c;

        return null;
    }

    /// <summary>Views a Control that <see cref="FindHandler"/> found as its handler.</summary>
    /// <param name="control">The Control.</param>
    /// <returns>The same object.</returns>
    static IPointerHandler Handler(Control control) => (IPointerHandler)control;

    /// <summary>Finds the first Control of a type among a Control and its ancestors.</summary>
    /// <typeparam name="T">The type to look for.</typeparam>
    /// <param name="control">The Control to start from.</param>
    /// <returns>The nearest match, or <see langword="null"/>.</returns>
    static T? Find<T>(Control? control)
        where T : Control
    {
        for (var c = control; c is not null; c = c.Parent as Control)
            if (c is T match)
                return match;

        return null;
    }
}
