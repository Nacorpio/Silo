namespace Silo;

/// <summary>
/// Tells the operating system what each point of a borderless window does, so that the system still moves and resizes it.
/// </summary>
/// <remarks>
/// A point on the outer border is a resize handle, unless the window is maximized. Otherwise a point is a drag area when the
/// Control under it has <see cref="Control.IsWindowDragArea"/>, and a client point for every other Control, so buttons and
/// menus in the title bar keep working.
/// </remarks>
public static class WindowHitTester
{
    /// <summary>The width of the resize border in logical pixels.</summary>
    public const double ResizeBorder = 6;

    /// <summary>Classifies a point.</summary>
    /// <param name="root">The root Control of the window.</param>
    /// <param name="x">The horizontal position in the window.</param>
    /// <param name="y">The vertical position in the window.</param>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
    /// <param name="canResize">Whether the window can be resized, which is not so while it is maximized.</param>
    /// <returns>What the point is.</returns>
    public static WindowRegion Test(Control root, double x, double y, double width, double height, bool canResize)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (canResize)
        {
            var left = x < ResizeBorder;
            var right = x >= width - ResizeBorder;
            var top = y < ResizeBorder;
            var bottom = y >= height - ResizeBorder;

            if (top && left)
                return WindowRegion.ResizeTopLeft;

            if (top && right)
                return WindowRegion.ResizeTopRight;

            if (bottom && left)
                return WindowRegion.ResizeBottomLeft;

            if (bottom && right)
                return WindowRegion.ResizeBottomRight;

            if (top)
                return WindowRegion.ResizeTop;

            if (bottom)
                return WindowRegion.ResizeBottom;

            if (left)
                return WindowRegion.ResizeLeft;

            if (right)
                return WindowRegion.ResizeRight;
        }

        return root.HitTest(x, y) is { IsWindowDragArea: true } ? WindowRegion.Drag : WindowRegion.Client;
    }
}
