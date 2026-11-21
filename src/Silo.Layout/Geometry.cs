namespace Silo.Layout;

/// <summary>A width and a height in device-independent pixels.</summary>
/// <param name="Width">The horizontal extent. Layout never produces a negative value.</param>
/// <param name="Height">The vertical extent. Layout never produces a negative value.</param>
public readonly record struct Size(double Width, double Height);

/// <summary>A rectangle in device-independent pixels.</summary>
/// <param name="X">The horizontal position of the left edge.</param>
/// <param name="Y">The vertical position of the top edge.</param>
/// <param name="Width">The horizontal extent.</param>
/// <param name="Height">The vertical extent.</param>
/// <remarks>
/// A rectangle that layout assigns to a node is its border box, relative to the border box of its parent. The root's rectangle
/// starts at 0, 0.
/// </remarks>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    /// <summary>Gets the position of the right edge, <c>X + Width</c>.</summary>
    public double Right => X + Width;

    /// <summary>Gets the position of the bottom edge, <c>Y + Height</c>.</summary>
    public double Bottom => Y + Height;

    /// <summary>Gets the extent of the rectangle.</summary>
    public Size Size => new(Width, Height);
}
