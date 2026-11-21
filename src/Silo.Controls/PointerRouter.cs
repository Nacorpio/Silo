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
public sealed class PointerRouter(Control root)
{
    /// <summary>The Control that the pointer is over, or <see langword="null"/> when it is outside the tree.</summary>
    Control? hovered;

    /// <summary>The Button that the pointer went down on and has not come up from yet.</summary>
    Button? pressed;

    /// <summary>Gets the Control that the pointer is currently over.</summary>
    public Control? Hovered => hovered;

    /// <summary>Moves the pointer.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Move(double x, double y) => SetHovered(root.HitTest(x, y));

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

        pressed = FindButton(hovered);

        return (pressed?.PointerDown() ?? false) | changed;
    }

    /// <summary>Releases the primary pointer button.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed. A <see cref="Button.Click"/> handler has run by then.</returns>
    public bool Up(double x, double y)
    {
        var changed = Move(x, y);

        var button = pressed;

        pressed = null;

        return (button?.PointerUp(FindButton(hovered) == button) ?? false) | changed;
    }

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

    /// <summary>Finds the Button that is a Control or one of its ancestors.</summary>
    /// <param name="control">The Control to start from.</param>
    /// <returns>The nearest Button, or <see langword="null"/>.</returns>
    static Button? FindButton(Control? control)
    {
        for (var c = control; c is not null; c = c.Parent as Control)
            if (c is Button button)
                return button;

        return null;
    }
}
