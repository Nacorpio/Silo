using Silo.Layout;
using Silo.Rendering;

namespace Silo.Studio;

/// <summary>
/// A line of text that takes the width it is given and ends with an ellipsis when the text is wider. It asks for no width of
/// its own, so a long text never widens the Control it sits in.
/// </summary>
public sealed class TrimmedText : Control
{
    /// <summary>Gets or sets the text. The default is an empty string.</summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string TypeName => "TextBlock";

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth) => new(0, Text.Length == 0 ? 0 : Measurer().Measure(Text, CurrentTextStyle()).Height);

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var shown = TextTrimming.Trim(Measurer(), Text, style, content.Width);

        if (shown.Length == 0)
            return;

        var size = Measurer().Measure(shown, style);

        context.DrawText(shown, content.X, content.Y + Math.Max(0, content.Height - size.Height) / 2, style, GetValue(ColorProperty));
    }
}
