using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// The one line of a <see cref="ToolCallCard"/> that sums up the arguments. It takes the room that its siblings leave and cuts the text with an ellipsis.
/// </summary>
/// <remarks>It measures as no width at all, so that a long summary never pushes the duration and the buttons out of the row. Give it <c>flex-grow</c> in a theme.</remarks>
public sealed class ToolCallSummary : Control
{
    /// <summary>The text.</summary>
    string text = string.Empty;

    /// <summary>Gets or sets the text.</summary>
    public string Text
    {
        get => text;
        set => text = value ?? string.Empty;
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
        => Text.Length == 0 ? default : new Size(0, Measurer().LineHeight(CurrentTextStyle()));

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var shown = TextTrimming.Trim(measurer, Text, style, content.Width);
        var y = content.Y + Math.Max(0, content.Height - measurer.LineHeight(style)) / 2;

        if (shown.Length > 0)
            context.DrawText(shown, content.X, y, style, GetValue(ColorProperty));
    }
}
