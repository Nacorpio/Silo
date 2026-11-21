using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// The base of Controls that show a single line of text, such as TextBlock and Button.
/// </summary>
/// <remarks>
/// The text is sized from the <c>font-family</c>, <c>font-size</c> and <c>font-weight</c> properties with the measurer from
/// <see cref="Control.TextMeasurer"/>, drawn with the <c>color</c> property, and positioned in the content box according to
/// <c>text-align</c>. Wrapping and multiple lines arrive with Silo.Text.
/// </remarks>
public abstract class TextControl : Control
{
    /// <summary>The horizontal position of the text in the content box.</summary>
    public static readonly StyledProperty<TextAlign> TextAlignProperty = StyledProperty.Register<TextControl, TextAlign>("text-align", TextAlign.Left, ValueParsers.TextAlignment);

    /// <summary>The text to show.</summary>
    string text = string.Empty;

    /// <summary>
    /// Runs before the first text Control is created, so that <see cref="TextAlignProperty"/> is registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TextControl()
    {
    }

    /// <summary>Gets or sets the text. The default is an empty string.</summary>
    /// <remarks>Arrange the tree again after changing it, since the size of the Control depends on its text.</remarks>
    public string Text
    {
        get => text;
        set => text = value ?? string.Empty;
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        if (Text.Length == 0)
            return default;

        return Measurer().Measure(Text, CurrentTextStyle());
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (Text.Length == 0)
            return;

        var content = ContentBox(bounds);
        var size = Measurer().Measure(Text, CurrentTextStyle());

        var free = Math.Max(0, content.Width - size.Width);

        var x = content.X + GetValue(TextAlignProperty) switch
        {
            TextAlign.Center => free / 2,
            TextAlign.Right => free,

            _ => 0,
        };

        var y = content.Y + Math.Max(0, content.Height - size.Height) / 2;

        context.DrawText(Text, x, y, CurrentTextStyle(), GetValue(ColorProperty));
    }

    /// <summary>Builds the font description from the current property values.</summary>
    /// <returns>The text style used for both measuring and drawing.</returns>
    TextStyle CurrentTextStyle() => new(GetValue(FontFamilyProperty), GetValue(FontSizeProperty), GetValue(BoldProperty));

    /// <summary>Gets the text measurer, which must have been set on this Control or an ancestor.</summary>
    /// <returns>The measurer.</returns>
    /// <exception cref="InvalidOperationException">No measurer is set on this Control or any ancestor.</exception>
    ITextMeasurer Measurer() => TextMeasurer ?? throw new InvalidOperationException($"{TypeName} needs a text measurer. Set Control.TextMeasurer on the root.");
}
