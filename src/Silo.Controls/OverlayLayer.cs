using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;

namespace Silo;

/// <summary>
/// A stack of Controls that are drawn above the window content, such as menus, drop-down lists and tooltips.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Show"/> puts a Control at a position in the window, on top of everything shown before it. The Control keeps its
/// natural size unless it has a <c>width</c> or <c>height</c>, and is moved as far as needed to lie inside the window. It is
/// styled with the same Stylesheets as the window content.
/// </para>
/// <para>
/// Every overlay is backed by an invisible layer the size of the window. A pointer press on that layer, outside the
/// Control, is a press <em>outside</em>: <see cref="PointerRouter"/> closes the top overlay and does not pass the press on.
/// The window also closes the top overlay when Escape is pressed.
/// </para>
/// <para>
/// An overlay shown with <c>modal: false</c>, such as a tooltip, is only drawn: the pointer goes through it to what is
/// below, and it has no backdrop, so a press does not close it. Whoever shows it closes it.
/// </para>
/// <para>
/// A platform window owns one layer (<c>SdlWindow.Overlays</c>) and calls <see cref="ApplyStyles"/>, <see cref="Arrange"/>
/// and <see cref="Render"/> after it handled the window content. <see cref="Show"/> overwrites the Control's
/// <c>margin</c> and <c>flex-shrink</c>, which is how it is positioned.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var menu = new Box { Name = "menu" };
///
/// menu.Children.Add(new Button { Text = "Copy" });
/// window.Overlays.Show(menu, 120, 80);
///
/// window.Overlays.Close(menu);
/// </code>
/// </example>
public sealed class OverlayLayer
{
    /// <summary>The overlays from the bottom to the top.</summary>
    readonly List<OverlayHost> hosts = [];


    /// <summary>Raised after an overlay was shown or closed, so the owner can style and draw a new frame.</summary>
    public event EventHandler? Changed;


    /// <summary>Gets how many overlays are shown, modal or not.</summary>
    public int Count => hosts.Count;

    /// <summary>Gets how many of the overlays are modal.</summary>
    public int ModalCount => hosts.Count(host => host.IsModal);

    /// <summary>Gets or sets the object that measures text in the overlays.</summary>
    public ITextMeasurer? TextMeasurer { get; set; }


    /// <summary>Tells whether a Control is currently shown as an overlay.</summary>
    /// <param name="content">The Control to look for.</param>
    /// <returns><see langword="true"/> when it was passed to <see cref="Show"/> and not closed since.</returns>
    public bool Contains(Control content) => hosts.Any(host => host.Content == content);

    /// <summary>Shows a Control above everything else.</summary>
    /// <param name="content">The Control to show. It must not be part of another tree.</param>
    /// <param name="x">The horizontal position of its left edge, relative to the window. It is moved left when it would stick out.</param>
    /// <param name="y">The vertical position of its top edge, relative to the window. It is moved up when it would stick out.</param>
    /// <param name="modal">
    /// <see langword="true"/> (the default) to take the pointer and close on a press outside. <see langword="false"/> to only draw it.
    /// </param>
    /// <exception cref="InvalidOperationException">The Control is already shown, or has a parent.</exception>
    public void Show(Control content, double x, double y, bool modal = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (Contains(content) || content.Parent is not null)
            throw new InvalidOperationException("The Control is already part of a tree.");

        hosts.Add(new OverlayHost(content, x, y, modal) { TextMeasurer = TextMeasurer });

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a Control in the middle of the window with the window dimmed behind it.</summary>
    /// <param name="dialog">The Dialog to show. It must not be part of another tree.</param>
    /// <param name="dismiss">
    /// Runs when Escape is pressed. A press outside the Dialog is swallowed and does nothing, so the Dialog stays until it is
    /// closed with <see cref="Close"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">The Dialog is already shown, or has a parent.</exception>
    public void ShowDialog(Dialog dialog, Action dismiss)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(dismiss);

        if (Contains(dialog) || dialog.Parent is not null)
            throw new InvalidOperationException("The Control is already part of a tree.");

        hosts.Add(new OverlayHost(dialog, 0, 0, modal: true, dismiss) { TextMeasurer = TextMeasurer });

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes an overlay.</summary>
    /// <param name="content">The Control that was passed to <see cref="Show"/>.</param>
    /// <returns><see langword="true"/> when it was shown.</returns>
    public bool Close(Control content)
    {
        var host = hosts.Find(host => host.Content == content);

        if (host is null)
            return false;

        host.Children.Clear();
        hosts.Remove(host);

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Closes the modal overlay that was shown last, or dismisses it when it is a Dialog.</summary>
    /// <returns><see langword="true"/> when there was one.</returns>
    public bool CloseTop()
    {
        if (hosts.FindLast(host => host.IsModal) is not { } top)
            return false;

        if (top.Dismiss is { } dismiss)
            dismiss();
        else
            Close(top.Content);

        return true;
    }

    /// <summary>Handles a press on the backdrop behind the top overlay.</summary>
    /// <returns>
    /// <see langword="true"/> when overlays closed or a Dialog was dismissed. A press outside menus closes all of them, down to
    /// a Dialog if there is one. A Dialog stays open, and the press is swallowed, unless its
    /// <see cref="Dialog.DismissOnOutsidePressProperty"/> is set.
    /// </returns>
    public bool PressOutside()
    {
        if (hosts.FindLast(host => host.IsModal) is not { } top)
            return false;

        if (top.Dismiss is null)
        {
            while (hosts.FindLast(host => host.IsModal) is { Dismiss: null } host)
                Close(host.Content);

            return true;
        }

        if (top.Content is not Dialog dialog || !dialog.GetValue(Dialog.DismissOnOutsidePressProperty))
            return false;

        top.Dismiss();

        return true;
    }

    /// <summary>Sends a key press to the modal overlay that was shown last.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys that were held down.</param>
    /// <returns><see langword="true"/> when the overlay changed, so a new frame is needed.</returns>
    /// <remarks>While a modal overlay is shown it takes the keys, so the Control that has the keyboard focus does not get them.</remarks>
    public bool KeyDown(Key key, KeyModifiers modifiers)
        => hosts.FindLast(host => host.IsModal) is { } top && top.Content.KeyDown(key, modifiers);

    /// <summary>Closes every overlay.</summary>
    /// <returns><see langword="true"/> when there was at least one.</returns>
    public bool CloseAll()
    {
        var any = hosts.Count > 0;

        foreach (var host in hosts.ToArray())
            Close(host.Content);

        return any;
    }

    /// <summary>Applies Stylesheets to every overlay.</summary>
    /// <param name="resolver">The Stylesheets.</param>
    public void ApplyStyles(StyleResolver resolver)
    {
        foreach (var host in hosts)
            host.ApplyStyles(resolver);
    }

    /// <summary>Lays out every overlay.</summary>
    /// <param name="available">The size of the window.</param>
    public void Arrange(Size available)
    {
        foreach (var host in hosts)
        {
            host.Arrange(available);

            if (host.Fit(available))
                host.Arrange(available);
        }
    }

    /// <summary>Paints every overlay, the first one shown at the bottom.</summary>
    /// <param name="context">The context to draw on, with the window's origin.</param>
    /// <remarks>Call <see cref="Arrange"/> first.</remarks>
    public void Render(IDrawingContext context)
    {
        foreach (var host in hosts)
            host.Render(context);
    }

    /// <summary>Finds the topmost Control of the modal overlays at a point.</summary>
    /// <param name="x">The horizontal position, relative to the window.</param>
    /// <param name="y">The vertical position, relative to the window.</param>
    /// <returns>
    /// The Control at the point, the invisible backdrop when the point is outside every overlay (see <see cref="IsBackdrop"/>),
    /// or <see langword="null"/> when no modal overlay is shown.
    /// </returns>
    /// <remarks>
    /// A menu opened from another menu does not hide it: a point on a lower menu hits that menu. A Dialog hides everything
    /// below it.
    /// </remarks>
    public Control? HitTest(double x, double y)
    {
        if (hosts.FindLast(host => host.IsModal) is not { } top)
            return null;

        for (var i = hosts.Count - 1; i >= 0; i--)
        {
            if (!hosts[i].IsModal)
                continue;

            if (hosts[i].HitTest(x, y) is { } hit && hit is not OverlayHost)
                return hit;

            if (hosts[i].Dismiss is not null)
                break;
        }

        return top.HitTest(x, y) ?? top;
    }

    /// <summary>Tells whether a hit is the invisible backdrop behind an overlay.</summary>
    /// <param name="hit">A Control returned by <see cref="HitTest"/>.</param>
    /// <returns><see langword="true"/> when the point was outside the overlay itself.</returns>
    public bool IsBackdrop(Control? hit) => hit is OverlayHost;
}
