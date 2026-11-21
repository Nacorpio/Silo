using Silo.Layout;

namespace Silo;

/// <summary>
/// The invisible Control that fills the window behind one overlay, and positions the overlay with its margin.
/// </summary>
sealed class OverlayHost : Control
{
    /// <summary>Creates a host around a Control.</summary>
    /// <param name="content">The overlay's Control.</param>
    public OverlayHost(Control content)
    {
        Content = content;

        SetValue(AlignItemsProperty, AlignItems.Start);
        Children.Add(content);
    }

    /// <summary>Gets the overlay's Control.</summary>
    public Control Content { get; }

    /// <inheritdoc/>
    protected override FlexDirection LayoutDirection => FlexDirection.Column;
}
