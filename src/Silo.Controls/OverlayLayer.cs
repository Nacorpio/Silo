using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A stack of Controls that are drawn above the window content, such as menus, drop-down lists and tooltips.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Show"/> puts a Control at a position in the window, on top of everything shown before it. The Control keeps its
/// natural size unless it has a <c>width</c> or <c>height</c>. It is styled with the same Stylesheets as the window content.
/// </para>
/// <para>
/// Every overlay is backed by an invisible layer the size of the window. A pointer press on that layer, outside the
/// Control, is a press <em>outside</em>: <see cref="PointerRouter"/> closes the top overlay and does not pass the press on.
/// The window also closes the top overlay when Escape is pressed.
/// </para>
/// <para>
/// A platform window owns one layer (<c>SdlWindow.Overlays</c>) and calls <see cref="ApplyStyles"/>, <see cref="Arrange"/>
/// and <see cref="Render"/> after it handled the window content. <see cref="Show"/> overwrites the Control's
/// <c>margin</c>, which is how it is positioned.
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


    /// <summary>Gets how many overlays are shown.</summary>
    public int Count => hosts.Count;

    /// <summary>Gets or sets the object that measures text in the overlays.</summary>
    public ITextMeasurer? TextMeasurer { get; set; }


    /// <summary>Tells whether a Control is currently shown as an overlay.</summary>
    /// <param name="content">The Control to look for.</param>
    /// <returns><see langword="true"/> when it was passed to <see cref="Show"/> and not closed since.</returns>
    public bool Contains(Control content) => hosts.Any(host => host.Content == content);

    /// <summary>Shows a Control above everything else.</summary>
    /// <param name="content">The Control to show. It must not be part of another tree.</param>
    /// <param name="x">The horizontal position of its left edge, relative to the window.</param>
    /// <param name="y">The vertical position of its top edge, relative to the window.</param>
    /// <exception cref="InvalidOperationException">The Control is already shown, or has a parent.</exception>
    public void Show(Control content, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (Contains(content) || content.Parent is not null)
            throw new InvalidOperationException("The Control is already part of a tree.");

        var host = new OverlayHost(content) { TextMeasurer = TextMeasurer };

        content.SetValue(Control.MarginProperty, new Thickness(Px(y), Px(0), Px(0), Px(x)));

        hosts.Add(host);
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

    /// <summary>Closes the overlay that was shown last.</summary>
    /// <returns><see langword="true"/> when there was one.</returns>
    public bool CloseTop() => hosts.Count > 0 && Close(hosts[^1].Content);

    /// <summary>Closes every overlay.</summary>
    /// <returns><see langword="true"/> when there was at least one.</returns>
    public bool CloseAll()
    {
        var any = hosts.Count > 0;

        while (CloseTop())
        {
        }

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
            host.Arrange(available);
    }

    /// <summary>Paints every overlay, the first one shown at the bottom.</summary>
    /// <param name="context">The context to draw on, with the window's origin.</param>
    /// <remarks>Call <see cref="Arrange"/> first.</remarks>
    public void Render(IDrawingContext context)
    {
        foreach (var host in hosts)
            host.Render(context);
    }

    /// <summary>Finds the topmost Control of the overlays at a point.</summary>
    /// <param name="x">The horizontal position, relative to the window.</param>
    /// <param name="y">The vertical position, relative to the window.</param>
    /// <returns>
    /// The Control at the point, the invisible backdrop when the point is outside every overlay (see <see cref="IsBackdrop"/>),
    /// or <see langword="null"/> when nothing is shown.
    /// </returns>
    public Control? HitTest(double x, double y)
        => hosts.Count == 0 ? null : hosts[^1].HitTest(x, y) ?? hosts[^1];

    /// <summary>Tells whether a hit is the invisible backdrop behind an overlay.</summary>
    /// <param name="hit">A Control returned by <see cref="HitTest"/>.</param>
    /// <returns><see langword="true"/> when the point was outside the overlay itself.</returns>
    public bool IsBackdrop(Control? hit) => hit is OverlayHost;

    /// <summary>Makes a length in pixels.</summary>
    /// <param name="value">The number of pixels.</param>
    /// <returns>The length.</returns>
    static Length Px(double value) => new(value, LengthUnit.Px);
}
