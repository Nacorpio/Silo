using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// A Button that toggles between on and off like a light switch: a pill-shaped track with a thumb at one end.
/// </summary>
/// <example>
/// <code language="csharp">
/// var dark = new ToggleSwitch { Text = "Dark mode" };
///
/// dark.CheckedChanged += (_, _) => theme.Dark = dark.IsChecked;
/// </code>
/// </example>
public class ToggleSwitch : ToggleButton
{
    /// <inheritdoc/>
    protected override Size IndicatorSize => new(32, 18);

    /// <inheritdoc/>
    protected override void DrawIndicator(IDrawingContext context, Rect box)
    {
        var radius = box.Height / 2;

        context.FillRect(box, GetValue(IndicatorBackgroundProperty), radius);
        context.StrokeRect(box, GetValue(IndicatorBorderColorProperty), 1, radius);

        var diameter = box.Height - 6;
        var x = IsChecked ? box.Right - 3 - diameter : box.X + 3;

        var thumb = new Rect(x, box.Y + 3, diameter, diameter);
        var color = GetValue(IsChecked ? IndicatorMarkColorProperty : IndicatorBorderColorProperty);

        context.FillRect(thumb, color, diameter / 2);
    }
}
