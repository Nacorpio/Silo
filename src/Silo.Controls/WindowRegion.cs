namespace Silo;

/// <summary>What a point of a window with a custom title bar means to the operating system.</summary>
public enum WindowRegion
{
    /// <summary>An ordinary point: pointer events go to the Controls.</summary>
    Client,

    /// <summary>A point that drags the window, and that maximizes it on a double press.</summary>
    Drag,

    /// <summary>The top edge, which resizes upward.</summary>
    ResizeTop,

    /// <summary>The bottom edge.</summary>
    ResizeBottom,

    /// <summary>The left edge.</summary>
    ResizeLeft,

    /// <summary>The right edge.</summary>
    ResizeRight,

    /// <summary>The top left corner.</summary>
    ResizeTopLeft,

    /// <summary>The top right corner.</summary>
    ResizeTopRight,

    /// <summary>The bottom left corner.</summary>
    ResizeBottomLeft,

    /// <summary>The bottom right corner.</summary>
    ResizeBottomRight,
}
