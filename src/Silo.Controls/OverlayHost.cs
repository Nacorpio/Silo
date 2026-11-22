using Silo.Layout;
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
    public OverlayHost(Control content, double x, double y, bool modal)
    {
        Content = content;
        IsModal = modal;

        SetValue(AlignItemsProperty, AlignItems.Start);
        content.SetValue(FlexShrinkProperty, 0.0);
        Children.Add(content);

        requested = (x, y);

        Place(x, y);
    }


    /// <summary>Gets the overlay's Control.</summary>
    public Control Content { get; }

    /// <summary>Gets whether the overlay takes the pointer. A passive overlay, such as a tooltip, is only drawn.</summary>
    public bool IsModal { get; }

    /// <inheritdoc/>
    protected override FlexDirection LayoutDirection => FlexDirection.Column;

    /// <summary>
    /// Moves the overlay so that it lies inside the window, as far as it fits. Call it after <see cref="Control.Arrange"/>.
    /// </summary>
    /// <param name="available">The size of the window.</param>
    /// <returns><see langword="true"/> when the overlay moved, so the host needs to be arranged again.</returns>
    public bool Fit(Size available)
    {
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
