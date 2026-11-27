using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// The little downward triangle at the right edge of the Controls that drop something down, such as ComboBox and DatePicker.
/// </summary>
internal static class DropDownArrow
{
    /// <summary>The width that the arrow takes at the right edge of the content box, in pixels.</summary>
    public const double Space = 24;


    /// <summary>Draws the arrow in the <see cref="Space"/> at the right edge of the content box.</summary>
    /// <param name="context">The context to draw with.</param>
    /// <param name="content">The content box of the Control.</param>
    /// <param name="color">The color of the arrow.</param>
    public static void Draw(IDrawingContext context, Rect content, Color color)
    {
        const int rows = 4;

        var area = new Rect(content.Right - Space, content.Y, Space, content.Height);
        var top = area.Y + ((area.Height - rows) / 2);

        for (var i = 0; i < rows; i++)
        {
            var width = 8 - (i * 2);
            var row = new Rect(area.X + ((area.Width - width) / 2), top + i, width, 1);

            context.FillRect(row, color);
        }
    }
}
