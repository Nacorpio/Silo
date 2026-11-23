using Silo.Layout;
using Silo.Styling.Values;

namespace Silo.Rendering;

/// <summary>
/// One recorded drawing operation of a <see cref="DisplayList"/>.
/// </summary>
/// <remarks>Commands are immutable records, so a display list can be handed to another thread without copying.</remarks>
public abstract record DrawCommand
{
    /// <summary>Performs the operation on a drawing context.</summary>
    /// <param name="context">The context to draw on.</param>
    public abstract void Execute(IDrawingContext context);
}

/// <summary>Records <see cref="IDrawingContext.Save"/>.</summary>
public sealed record SaveCommand : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.Save();
}

/// <summary>Records <see cref="IDrawingContext.Restore"/>.</summary>
public sealed record RestoreCommand : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.Restore();
}

/// <summary>Records <see cref="IDrawingContext.Translate"/>.</summary>
/// <param name="Dx">The horizontal offset.</param>
/// <param name="Dy">The vertical offset.</param>
public sealed record TranslateCommand(double Dx, double Dy) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.Translate(Dx, Dy);
}

/// <summary>Records <see cref="IDrawingContext.ClipRect"/>.</summary>
/// <param name="Rect">The clip rectangle.</param>
public sealed record ClipRectCommand(Rect Rect) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.ClipRect(Rect);
}

/// <summary>Records <see cref="IDrawingContext.FillRect"/>.</summary>
/// <param name="Rect">The rectangle to fill.</param>
/// <param name="Color">The fill color.</param>
/// <param name="CornerRadius">The corner radius in pixels.</param>
public sealed record FillRectCommand(Rect Rect, Color Color, double CornerRadius) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.FillRect(Rect, Color, CornerRadius);
}

/// <summary>Records <see cref="IDrawingContext.ClipRoundRect"/>.</summary>
/// <param name="Rect">The clip rectangle.</param>
/// <param name="CornerRadius">The corner radius in pixels.</param>
public sealed record ClipRoundRectCommand(Rect Rect, double CornerRadius) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.ClipRoundRect(Rect, CornerRadius);
}

/// <summary>Records <see cref="IDrawingContext.StrokePolyline"/>.</summary>
/// <param name="Points">The points of the line.</param>
/// <param name="Color">The line color.</param>
/// <param name="Width">The line width in pixels.</param>
/// <param name="Closed">Whether the last point joins the first.</param>
public sealed record StrokePolylineCommand(IReadOnlyList<(double X, double Y)> Points, Color Color, double Width, bool Closed) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.StrokePolyline(Points, Color, Width, Closed);
}

/// <summary>Records <see cref="IDrawingContext.FillGradient"/>.</summary>
/// <param name="Rect">The rectangle to fill.</param>
/// <param name="From">The color at the start edge.</param>
/// <param name="To">The color at the end edge.</param>
/// <param name="Vertical">Whether the gradient runs from top to bottom instead of left to right.</param>
public sealed record FillGradientCommand(Rect Rect, Color From, Color To, bool Vertical) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.FillGradient(Rect, From, To, Vertical);
}

/// <summary>Records <see cref="IDrawingContext.StrokeRect"/>.</summary>
/// <param name="Rect">The rectangle to outline.</param>
/// <param name="Color">The stroke color.</param>
/// <param name="Width">The stroke width in pixels.</param>
/// <param name="CornerRadius">The corner radius in pixels.</param>
public sealed record StrokeRectCommand(Rect Rect, Color Color, double Width, double CornerRadius) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.StrokeRect(Rect, Color, Width, CornerRadius);
}

/// <summary>Records <see cref="IDrawingContext.DrawText"/>.</summary>
/// <param name="Text">The text to draw.</param>
/// <param name="X">The left edge of the line box.</param>
/// <param name="Y">The top edge of the line box.</param>
/// <param name="Style">The font to use.</param>
/// <param name="Color">The text color.</param>
public sealed record DrawTextCommand(string Text, double X, double Y, TextStyle Style, Color Color) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.DrawText(Text, X, Y, Style, Color);
}

/// <summary>Records <see cref="IDrawingContext.DrawMask"/>.</summary>
/// <param name="Image">The image whose transparency is used.</param>
/// <param name="Destination">The rectangle it fills.</param>
/// <param name="Color">The color of the opaque parts.</param>
public sealed record DrawMaskCommand(IImage Image, Rect Destination, Color Color) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.DrawMask(Image, Destination, Color);
}

/// <summary>Records <see cref="IDrawingContext.DrawImage"/>.</summary>
/// <param name="Image">The image to draw.</param>
/// <param name="Destination">The rectangle it fills.</param>
public sealed record DrawImageCommand(IImage Image, Rect Destination) : DrawCommand
{
    /// <inheritdoc/>
    public override void Execute(IDrawingContext context) => context.DrawImage(Image, Destination);
}
