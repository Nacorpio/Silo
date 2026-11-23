using Silo.Layout;

namespace Silo;

/// <summary>
/// A container that keeps its content at a fixed width-to-height ratio, as large as fits and centered, whatever the size of
/// the box itself.
/// </summary>
/// <remarks>
/// <para>
/// Give the box its size with <c>width</c> and <c>height</c> or let a parent stretch it. The <see cref="Content"/> is then
/// placed in the biggest rectangle of the <see cref="Ratio"/> that fits inside the content box (the area inside padding and border), centered on
/// both axes, so the leftover space shows as bars on the two sides or above and below. Use it for a video frame, a thumbnail or a
/// chart that must stay square.
/// </para>
/// <para>
/// The box does not compute its own height from its width: size it explicitly or through the parent's layout.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var frame = new AspectRatioBox { Ratio = 16.0 / 9, Content = new Image { Source = picture } };
/// </code>
/// </example>
public class AspectRatioBox : Control
{
    Control? content;

    double ratio = 1;

    /// <summary>Gets or sets the Control that is kept at the <see cref="Ratio"/>, or <see langword="null"/> for none.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            if (content is not null)
                Children.Remove(content);

            content = value;

            if (content is not null)
                Children.Add(content);
        }
    }

    /// <summary>
    /// Gets or sets the width divided by the height of the content, such as <c>16.0 / 9</c>. A value that is not above zero
    /// is ignored and the ratio stays as it was. The default is 1, a square.
    /// </summary>
    public double Ratio
    {
        get => ratio;
        set
        {
            if (value > 0 && double.IsFinite(value))
                ratio = value;
        }
    }

    /// <summary>Finds the biggest rectangle of a ratio that fits in an area, centered in it.</summary>
    /// <param name="area">The area to fit into.</param>
    /// <param name="ratio">The width divided by the height.</param>
    /// <returns>The rectangle, with the same center as the area.</returns>
    public static Rect Fit(Rect area, double ratio)
    {
        var width = Math.Min(area.Width, area.Height * ratio);
        var height = width / ratio;

        return new Rect(area.X + ((area.Width - width) / 2), area.Y + ((area.Height - height) / 2), width, height);
    }

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect area)
    {
        foreach (var child in Children)
            PlaceChild((Control)child, Fit(area, ratio));

        return true;
    }
}
