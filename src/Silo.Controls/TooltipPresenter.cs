namespace Silo;

/// <summary>
/// Shows a <see cref="Tooltip"/> for the Control that the pointer has rested on, and hides it again.
/// </summary>
/// <remarks>
/// <para>
/// The presenter has no timer of its own. The platform window tells it where the pointer is with <see cref="Hover"/>, asks
/// it to show what is due with <see cref="Tick"/>, and hides the tooltip on any press, key or wheel turn with
/// <see cref="Dismiss"/>. Every method takes the current time as the time since any fixed start, so it can be tested without
/// waiting.
/// </para>
/// <para>
/// The pointer has to rest on the same Control for <see cref="Delay"/> (500 ms by default). The tooltip appears below and
/// right of the pointer, moved to lie inside the window, and it stays while the pointer moves inside the same Control. No
/// tooltip is shown while a modal overlay, such as a menu, is open.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tooltips = new TooltipPresenter(window.Overlays);
///
/// tooltips.Hover(router.Hovered, x, y, clock.Elapsed);
///
/// if (tooltips.Tick(clock.Elapsed))
///     window.Invalidate();
/// </code>
/// </example>
/// <param name="overlays">The layer to show tooltips in.</param>
public sealed class TooltipPresenter(OverlayLayer overlays)
{
    /// <summary>The space between the pointer and the tooltip's left edge in pixels.</summary>
    const double OffsetX = 12;

    /// <summary>The space between the pointer and the tooltip's top edge in pixels.</summary>
    const double OffsetY = 20;


    /// <summary>The Control whose tooltip is waiting or shown.</summary>
    Control? target;

    /// <summary>The tooltip that is shown, or <see langword="null"/>.</summary>
    Tooltip? shown;

    /// <summary>When the pointer came to rest on <see cref="target"/>.</summary>
    TimeSpan since;

    /// <summary>The last pointer position.</summary>
    (double X, double Y) pointer;


    /// <summary>Gets or sets how long the pointer has to rest on a Control before its tooltip shows. The default is 500 ms.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets the tooltip that is shown, or <see langword="null"/>.</summary>
    public Tooltip? Current => shown;

    /// <summary>Gets whether a tooltip is shown.</summary>
    public bool IsShown => shown is not null;


    /// <summary>Tells the presenter where the pointer is.</summary>
    /// <param name="hovered">The Control under the pointer, or <see langword="null"/>.</param>
    /// <param name="x">The horizontal position, relative to the window.</param>
    /// <param name="y">The vertical position, relative to the window.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when a tooltip was hidden, so a new frame is needed.</returns>
    public bool Hover(Control? hovered, double x, double y, TimeSpan now)
    {
        var next = TooltipOwner(hovered);

        pointer = (x, y);

        if (next == target)
            return false;

        var hidden = Dismiss();

        target = next;
        since = now;

        return hidden;
    }

    /// <summary>Shows the tooltip when the pointer has rested long enough.</summary>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when a tooltip was shown, so a new frame is needed.</returns>
    public bool Tick(TimeSpan now)
    {
        if (target is null || shown is not null || now - since < Delay || overlays.ModalCount > 0)
            return false;

        shown = new Tooltip { Text = target.Tooltip ?? string.Empty, Shortcut = target.TooltipShortcut ?? string.Empty };

        overlays.Show(shown, pointer.X + OffsetX, pointer.Y + OffsetY, modal: false);

        return true;
    }

    /// <summary>Finds how long until a tooltip is due.</summary>
    /// <param name="now">The current time.</param>
    /// <returns>The time left, or <see langword="null"/> when no tooltip is waiting.</returns>
    public TimeSpan? DueIn(TimeSpan now)
        => target is null || shown is not null ? null : (since + Delay - now) is { Ticks: > 0 } left ? left : TimeSpan.Zero;

    /// <summary>Hides the tooltip and forgets the Control, so nothing shows again until the pointer moves to a Control.</summary>
    /// <returns><see langword="true"/> when a tooltip was shown.</returns>
    public bool Dismiss()
    {
        var tip = shown;

        shown = null;
        target = null;

        return tip is not null && overlays.Close(tip);
    }

    /// <summary>Finds the nearest Control with a tooltip, starting at a Control and going up.</summary>
    /// <param name="control">The Control under the pointer.</param>
    /// <returns>The Control, or <see langword="null"/> when none has a tooltip.</returns>
    static Control? TooltipOwner(Control? control)
    {
        for (var c = control; c is not null; c = c.Parent as Control)
            if (!string.IsNullOrEmpty(c.Tooltip))
                return c;

        return null;
    }
}
