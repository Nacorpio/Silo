using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>The part of a color that a <see cref="ColorSurface"/> edits.</summary>
public enum ColorSurfaceKind
{
    /// <summary>A square with the saturation along the horizontal axis and the value (brightness) along the vertical one.</summary>
    SaturationValue,

    /// <summary>A bar that runs through the hues, from red to red again.</summary>
    Hue,

    /// <summary>A bar that fades from transparent to the opaque color, in front of a checkerboard.</summary>
    Alpha,
}

/// <summary>
/// A surface of a <see cref="ColorPanel"/> that is painted with a color gradient and edited by dragging a thumb on it.
/// </summary>
/// <remarks>
/// <para>
/// It holds a position, <see cref="X"/> and <see cref="Y"/>, each from 0 to 1, and does not know which color that is: the
/// ColorPanel sets <see cref="Hue"/> and <see cref="BaseColor"/> for the painting and reads the position back when
/// <see cref="Changed"/> is raised. The bars use only <see cref="X"/>. Give it a <c>height</c>, since it has no content to
/// size itself from.
/// </para>
/// <para>
/// The arrows move the thumb by 0.01, or by 0.1 with Shift. On a bar only Left and Right work, and Home and End go to the
/// ends. The <see cref="PointerRouter"/> drives it through <see cref="IPointerHandler"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var square = new ColorSurface(ColorSurfaceKind.SaturationValue) { Hue = 210 };
///
/// square.Changed += (_, _) => Console.WriteLine($"saturation {square.X}, value {1 - square.Y}");
/// </code>
/// </example>
public class ColorSurface : Control, IPointerHandler
{
    /// <summary>The diameter of the thumb in pixels.</summary>
    const double ThumbSize = 12;

    /// <summary>The size of one square of the checkerboard in pixels.</summary>
    const double Cell = 6;

    /// <summary>The distance the arrows move the thumb.</summary>
    const double Step = 0.01;

    /// <summary>The distance the arrows move the thumb while Shift is held.</summary>
    const double BigStep = 0.1;


    /// <summary>The colors of the hue bar, from red around to red.</summary>
    static readonly Color[] HueStops =
    [
        new(255, 0, 0), new(255, 255, 0), new(0, 255, 0), new(0, 255, 255), new(0, 0, 255), new(255, 0, 255), new(255, 0, 0),
    ];


    /// <summary>The horizontal position.</summary>
    double x;

    /// <summary>The vertical position.</summary>
    double y;

    /// <summary>The hue of the square in degrees.</summary>
    double hue;

    /// <summary>The color that the alpha bar fades to.</summary>
    Color baseColor = new(0, 0, 0);

    /// <summary>Whether the thumb is being dragged.</summary>
    bool dragging;


    /// <summary>
    /// Runs before the first ColorSurface is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ColorSurface()
    {
    }

    /// <summary>Creates a surface.</summary>
    /// <param name="kind">What it edits.</param>
    public ColorSurface(ColorSurfaceKind kind) => Kind = kind;

    /// <summary>Raised after the user moved the thumb, by dragging or with the keyboard. Setting the position from code does not raise it.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the pointer goes down on the surface, before the thumb moves.</summary>
    public event EventHandler? Pressed;

    /// <summary>Gets what the surface edits.</summary>
    public ColorSurfaceKind Kind { get; }

    /// <summary>Gets or sets the horizontal position, from 0 (left) to 1 (right). It is clamped.</summary>
    public double X
    {
        get => x;
        set => x = Math.Clamp(value, 0, 1);
    }

    /// <summary>Gets or sets the vertical position, from 0 (top) to 1 (bottom). It is clamped, and bars ignore it.</summary>
    public double Y
    {
        get => y;
        set => y = Math.Clamp(value, 0, 1);
    }

    /// <summary>Gets or sets the hue in degrees that a <see cref="ColorSurfaceKind.SaturationValue"/> square is painted with.</summary>
    public double Hue
    {
        get => hue;
        set => hue = value;
    }

    /// <summary>Gets or sets the opaque color that a <see cref="ColorSurfaceKind.Alpha"/> bar fades to.</summary>
    public Color BaseColor
    {
        get => baseColor;
        set => baseColor = value;
    }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public bool PointerDown(double x, double y)
    {
        dragging = true;

        Pressed?.Invoke(this, EventArgs.Empty);

        return MoveTo(x, y);
    }

    /// <inheritdoc/>
    public bool PointerDrag(double x, double y) => dragging && MoveTo(x, y);

    /// <inheritdoc/>
    public void PointerUp() => dragging = false;

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        var step = modifiers.HasFlag(KeyModifiers.Shift) ? BigStep : Step;
        var vertical = Kind == ColorSurfaceKind.SaturationValue;
        var (nextX, nextY) = (x, y);

        switch (key)
        {
            case Key.Left:
                nextX -= step;
                break;

            case Key.Right:
                nextX += step;
                break;

            case Key.Up when vertical:
                nextY -= step;
                break;

            case Key.Down when vertical:
                nextY += step;
                break;

            case Key.Home when !vertical:
                nextX = 0;
                break;

            case Key.End when !vertical:
                nextX = 1;
                break;

            default:
                return false;
        }

        return Move(nextX, nextY);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var area = new Rect(0, 0, bounds.Width, bounds.Height);

        context.Save();
        context.ClipRect(area);

        switch (Kind)
        {
            case ColorSurfaceKind.SaturationValue:
                context.FillGradient(area, new(255, 255, 255), ColorPanel.FromHsv(hue, 1, 1));
                context.FillGradient(area, new(0, 0, 0, 0), new(0, 0, 0), vertical: true);
                break;

            case ColorSurfaceKind.Hue:
                DrawHue(context, area);
                break;

            default:
                DrawChecker(context, area);
                context.FillGradient(area, new(baseColor.R, baseColor.G, baseColor.B, 0), baseColor with { A = 255 });
                break;
        }

        context.Restore();

        DrawThumb(context, area);
    }

    /// <summary>Moves the thumb to a pointer position.</summary>
    /// <param name="px">The horizontal position relative to the border box.</param>
    /// <param name="py">The vertical position relative to the border box.</param>
    /// <returns><see langword="true"/> when the thumb moved, so a new frame is needed.</returns>
    bool MoveTo(double px, double py)
    {
        var nextX = Bounds.Width > 0 ? px / Bounds.Width : 0;
        var nextY = Bounds.Height > 0 ? py / Bounds.Height : 0;

        return Move(nextX, nextY);
    }

    /// <summary>Sets the position and raises <see cref="Changed"/> when it changed.</summary>
    /// <param name="nextX">The new horizontal position.</param>
    /// <param name="nextY">The new vertical position. A bar keeps its own.</param>
    /// <returns><see langword="true"/> when the position changed.</returns>
    bool Move(double nextX, double nextY)
    {
        var (beforeX, beforeY) = (x, y);

        X = nextX;

        if (Kind == ColorSurfaceKind.SaturationValue)
            Y = nextY;

        if (beforeX == x && beforeY == y)
            return false;

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Paints the rainbow of the hue bar.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="area">The bar.</param>
    static void DrawHue(IDrawingContext context, Rect area)
    {
        var segments = HueStops.Length - 1;
        var width = area.Width / segments;

        for (var i = 0; i < segments; i++)
            context.FillGradient(new Rect(area.X + (i * width), area.Y, width + 1, area.Height), HueStops[i], HueStops[i + 1]);
    }

    /// <summary>Paints the checkerboard that shows through transparent colors.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="area">The bar.</param>
    static void DrawChecker(IDrawingContext context, Rect area)
    {
        context.FillRect(area, new(255, 255, 255));

        for (var column = 0; column * Cell < area.Width; column++)
            for (var row = 0; row * Cell < area.Height; row++)
                if ((column + row) % 2 == 1)
                    context.FillRect(new Rect(column * Cell, row * Cell, Cell, Cell), new(204, 204, 204));
    }

    /// <summary>Draws the thumb: a ring that is visible on light and on dark colors.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="area">The surface.</param>
    void DrawThumb(IDrawingContext context, Rect area)
    {
        var centerX = x * area.Width;
        var centerY = Kind == ColorSurfaceKind.SaturationValue ? y * area.Height : area.Height / 2;
        var thumb = new Rect(centerX - (ThumbSize / 2), centerY - (ThumbSize / 2), ThumbSize, ThumbSize);

        context.StrokeRect(thumb, new(0, 0, 0, 110), 3, ThumbSize / 2);
        context.StrokeRect(thumb, new(255, 255, 255), 2, ThumbSize / 2);
    }
}
