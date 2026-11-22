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

    /// <summary>The Slider that is being dragged.</summary>
    Slider? dragged;

    /// <summary>Gets the Control that the pointer is currently over.</summary>
    public Control? Hovered => hovered;

    /// <summary>Moves the pointer.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Move(double x, double y)
    {
        var moved = dragged?.PointerMove(dragged.ToLocal(x, y).X) ?? false;
        var hit = Hit(x, y);
        var changed = SetHovered(overlays?.IsBackdrop(hit) == true ? null : hit) | moved;

        if (Find<ListView>(hovered) is { HoverSelects: true } list)
            changed |= list.PointerMove(list.ToLocal(x, y).Y);

        return changed;
    }

    /// <summary>Tells the router that the pointer left the window.</summary>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Leave() => SetHovered(null);

    /// <summary>Presses the primary pointer button.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Down(double x, double y)
    {
        var changed = Move(x, y);

        if (overlays?.IsBackdrop(Hit(x, y)) == true)
            return overlays.CloseTop() | changed;

        pressed = Find<Button>(hovered);
        dragged = Find<Slider>(hovered);

        changed |= dragged?.PointerDown(dragged.ToLocal(x, y).X) ?? false;

        if (Find<TextBox>(hovered) is { } box)
            changed |= box.PointerDown(box.ToLocal(x, y).X);

        if (Find<ComboBox>(hovered) is { } combo)
            changed |= combo.PointerDown();

        if (Find<ListView>(hovered) is { } list)
            changed |= list.PointerDown(list.ToLocal(x, y).Y);

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
            return overlays.CloseTop() | changed;

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
        var slider = dragged;

        pressed = null;
        dragged = null;

        changed |= slider?.PointerUp() ?? false;

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
