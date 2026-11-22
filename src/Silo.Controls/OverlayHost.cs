using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// The invisible Control that fills the window behind one overlay, and positions the overlay with its margin.
/// </summary>
sealed class OverlayHost : Control
{
    /// <summary>Where the overlay was asked to be.</summary>
    (double X, double Y) requested;

    /// <summary>Where the overlay is, after it was moved to fit the window.</summary>
    (double X, double Y) placed;


    /// <summary>Creates a host around a Control.</summary>
    /// <param name="content">The overlay's Control.</param>
    /// <param name="x">The horizontal position of the overlay's left edge.</param>
    /// <param name="y">The vertical position of the overlay's top edge.</param>
    /// <param name="modal">Whether the overlay takes the pointer, with a backdrop behind it.</param>
    /// <param name="dismiss">
    /// For a Dialog, which is centered and dims the window: what to run when it is dismissed. Otherwise <see langword="null"/>.
    /// </param>
    public OverlayHost(Control content, double x, double y, bool modal, Action? dismiss = null)
    {
        Content = content;
        IsModal = modal;
        Dismiss = dismiss;

        SetValue(AlignItemsProperty, dismiss is null ? AlignItems.Start : AlignItems.Center);
        content.SetValue(FlexShrinkProperty, 0.0);
        Children.Add(content);

        if (dismiss is not null)
        {
            SetValue(JustifyContentProperty, JustifyContent.Center);

            return;
        }

        requested = (x, y);

        Place(x, y);
    }


    /// <summary>Gets the overlay's Control.</summary>
    public Control Content { get; }

    /// <summary>Gets what runs when a Dialog is dismissed, or <see langword="null"/> when the overlay is not a Dialog.</summary>
    public Action? Dismiss { get; }

    /// <summary>Gets whether the overlay takes the pointer. A passive overlay, such as a tooltip, is only drawn.</summary>
    public bool IsModal { get; }

    /// <inheritdoc/>
    protected override FlexDirection LayoutDirection => FlexDirection.Column;

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (Content is Dialog dialog && dialog.GetValue(Dialog.BackdropColorProperty) is { A: > 0 } color)
            context.FillRect(bounds, color);
    }

    /// <summary>
    /// Moves the overlay so that it lies inside the window, as far as it fits. Call it after <see cref="Control.Arrange"/>.
    /// </summary>
    /// <param name="available">The size of the window.</param>
    /// <returns><see langword="true"/> when the overlay moved, so the host needs to be arranged again.</returns>
    public bool Fit(Size available)
    {
        if (Dismiss is not null)
            return false;

        var x = Math.Max(0, Math.Min(requested.X, available.Width - Content.Bounds.Width));
        var y = Math.Max(0, Math.Min(requested.Y, available.Height - Content.Bounds.Height));

        if ((x, y) == placed)
            return false;

        Place(x, y);

        return true;
    }

    /// <summary>Sets the margin that puts the overlay at a position.</summary>
    /// <param name="x">The horizontal position of the overlay's left edge.</param>
    /// <param name="y">The vertical position of the overlay's top edge.</param>
    void Place(double x, double y)
    {
        placed = (x, y);

        Content.SetValue(MarginProperty, new Thickness(Px(y), Px(0), Px(0), Px(x)));
    }

    /// <summary>Makes a length in pixels.</summary>
    /// <param name="value">The number of pixels.</param>
    /// <returns>The length.</returns>
    static Length Px(double value) => new(value, LengthUnit.Px);
}
