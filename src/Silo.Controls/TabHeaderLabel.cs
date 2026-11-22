using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>Shows the icon and the title of a <see cref="TabHeader"/>, laid out by the header's icon properties.</summary>
/// <param name="owner">The header whose <c>icon-position</c>, <c>icon-size</c> and <c>icon-gap</c> are used.</param>
internal sealed class TabHeaderLabel(TabHeader owner) : Control
{
    /// <summary>Gets or sets the title.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the icon, or <see langword="null"/> for none.</summary>
    public IImage? Icon { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var (icon, text, gap) = Parts();

        return owner.GetValue(TabHeader.IconPositionProperty) == IconPosition.Top
            ? new Size(Math.Max(icon.Width, text.Width), icon.Height + gap + text.Height)
            : new Size(icon.Width + gap + text.Width, Math.Max(icon.Height, text.Height));
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var (icon, text, gap) = Parts();

        var position = owner.GetValue(TabHeader.IconPositionProperty);
        var style = CurrentTextStyle();
        var color = GetValue(ColorProperty);

        if (position == IconPosition.Top)
        {
            var height = icon.Height + gap + text.Height;
            var top = content.Y + Math.Max(0, content.Height - height) / 2;

            DrawIcon(context, content.X + ((content.Width - icon.Width) / 2), top, icon);

            if (Text.Length > 0)
                context.DrawText(Text, content.X + ((content.Width - text.Width) / 2), top + icon.Height + gap, style, color);

            return;
        }

        var iconFirst = position == IconPosition.Left;
        var x = content.X;
        var iconX = iconFirst ? x : x + text.Width + gap;
        var textX = iconFirst ? x + icon.Width + gap : x;

        DrawIcon(context, iconX, content.Y + ((content.Height - icon.Height) / 2), icon);

        if (Text.Length > 0)
            context.DrawText(Text, textX, content.Y + ((content.Height - text.Height) / 2), style, color);
    }

    /// <summary>Draws the icon scaled into a square.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="x">The left edge of the square.</param>
    /// <param name="y">The top edge of the square.</param>
    /// <param name="size">The size of the square, which is empty when there is no icon.</param>
    void DrawIcon(IDrawingContext context, double x, double y, Size size)
    {
        if (Icon is null || size.Width <= 0)
            return;

        var fitted = Image.Fit(Icon, new Rect(x, y, size.Width, size.Height));

        if (fitted.Width > 0)
            Image.Draw(context, Icon, fitted, GetValue(ColorProperty));
    }

    /// <summary>Measures the icon and the title.</summary>
    /// <returns>The size of the icon square and of the title, and the gap between them, which is 0 unless both exist.</returns>
    (Size Icon, Size Text, double Gap) Parts()
    {
        var side = Icon is null ? 0 : owner.GetValue(TabHeader.IconSizeProperty);
        var text = Text.Length == 0 ? default : Measurer().Measure(Text, CurrentTextStyle());
        var gap = side > 0 && text.Width > 0 ? owner.GetValue(TabHeader.IconGapProperty) : 0;

        return (new Size(side, side), text, gap);
    }
}
