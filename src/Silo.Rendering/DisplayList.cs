using Silo.Layout;
using Silo.Styling.Values;

namespace Silo.Rendering;

/// <summary>
/// An immutable recording of the drawing operations for one frame.
/// </summary>
/// <remarks>
/// The UI thread builds a display list with <see cref="DisplayListBuilder"/> and passes it to the render thread, which
/// replays it on a backend with <see cref="Replay"/>. Because neither the list nor its commands can change, no locking is needed.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var builder = new DisplayListBuilder();
///
/// builder.FillRect(new Rect(0, 0, 100, 40), new Color(59, 130, 246));
///
/// DisplayList frame = builder.Build();
///
/// frame.Replay(skiaContext);
/// </code>
/// </example>
public sealed class DisplayList
{
    /// <summary>Creates a display list that owns a copy of the commands.</summary>
    /// <param name="commands">The commands in drawing order.</param>
    internal DisplayList(IEnumerable<DrawCommand> commands) => Commands = commands.ToArray();

    /// <summary>Gets the recorded commands, in drawing order.</summary>
    public IReadOnlyList<DrawCommand> Commands { get; }

    /// <summary>Executes every command on a drawing context, in order.</summary>
    /// <param name="context">The context to draw on.</param>
    public void Replay(IDrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var command in Commands)
            command.Execute(context);
    }
}

/// <summary>
/// An <see cref="IDrawingContext"/> that records its calls instead of drawing.
/// </summary>
/// <remarks>Unlike a real backend, <see cref="Restore"/> only checks that a matching <see cref="Save"/> exists.</remarks>
public sealed class DisplayListBuilder : IDrawingContext
{
    /// <summary>The commands recorded so far.</summary>
    readonly List<DrawCommand> commands = [];

    /// <summary>The number of <see cref="Save"/> calls without a matching <see cref="Restore"/>.</summary>
    int depth;

    /// <summary>Gets the number of commands recorded so far.</summary>
    public int Count => commands.Count;

    /// <summary>Freezes the recorded commands into a display list.</summary>
    /// <returns>A list that is not affected by later calls on this builder.</returns>
    /// <exception cref="InvalidOperationException">A <see cref="Save"/> has no matching <see cref="Restore"/>.</exception>
    public DisplayList Build()
    {
        if (depth != 0)
            throw new InvalidOperationException($"{depth} Save call(s) have no matching Restore.");

        return new DisplayList(commands);
    }

    /// <inheritdoc/>
    public void Save()
    {
        depth++;

        commands.Add(new SaveCommand());
    }

    /// <inheritdoc/>
    public void Restore()
    {
        if (depth == 0)
            throw new InvalidOperationException("Restore was called without a matching Save.");

        depth--;

        commands.Add(new RestoreCommand());
    }

    /// <inheritdoc/>
    public void Translate(double dx, double dy) => commands.Add(new TranslateCommand(dx, dy));

    /// <inheritdoc/>
    public void ClipRect(Rect rect) => commands.Add(new ClipRectCommand(rect));

    /// <inheritdoc/>
    public void FillRect(Rect rect, Color color, double cornerRadius = 0)
        => commands.Add(new FillRectCommand(rect, color, cornerRadius));

    /// <inheritdoc/>
    public void ClipRoundRect(Rect rect, double cornerRadius) => commands.Add(new ClipRoundRectCommand(rect, cornerRadius));

    /// <inheritdoc/>
    public void FillPolygon(IReadOnlyList<(double X, double Y)> points, Color color)
        => commands.Add(new FillPolygonCommand([.. points], color));

    /// <inheritdoc/>
    public void StrokePolyline(IReadOnlyList<(double X, double Y)> points, Color color, double width, bool closed = false)
        => commands.Add(new StrokePolylineCommand([.. points], color, width, closed));

    /// <inheritdoc/>
    public void FillGradient(Rect rect, Color from, Color to, bool vertical = false)
        => commands.Add(new FillGradientCommand(rect, from, to, vertical));

    /// <inheritdoc/>
    public void StrokeRect(Rect rect, Color color, double width, double cornerRadius = 0)
        => commands.Add(new StrokeRectCommand(rect, color, width, cornerRadius));

    /// <inheritdoc/>
    public void DrawText(string text, double x, double y, TextStyle style, Color color)
        => commands.Add(new DrawTextCommand(text, x, y, style, color));

    /// <inheritdoc/>
    public void DrawImage(IImage image, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(image);

        commands.Add(new DrawImageCommand(image, destination));
    }

    /// <inheritdoc/>
    public void DrawMask(IImage image, Rect destination, Color color)
    {
        ArgumentNullException.ThrowIfNull(image);

        commands.Add(new DrawMaskCommand(image, destination, color));
    }
}
