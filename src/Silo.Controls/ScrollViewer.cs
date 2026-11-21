using Silo.Layout;

namespace Silo;

/// <summary>
/// Shows part of a taller child and scrolls it vertically. The child is clipped to the ScrollViewer's content box.
/// </summary>
/// <remarks>
/// <para>
/// A ScrollViewer lays its child out in a column at the child's natural height, so the child can be taller than the
/// viewport. Set the child through <see cref="Content"/>, which gives it a local <c>flex-shrink</c> of 0 so that the flex
/// engine does not squeeze it. Give the ScrollViewer a height, or a <c>flex-grow</c> inside a column, so that it has a viewport smaller than its content.
/// </para>
/// <para>
/// The platform layer scrolls it through <see cref="PointerRouter.Scroll"/> when the mouse wheel turns over it.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var list = new ScrollViewer { Name = "log" };
///
/// list.Content = longColumn;
///
/// list.ScrollBy(120);
/// </code>
/// </example>
public class ScrollViewer : Control, IScrollable
{
    /// <summary>How far the content has been scrolled up, in pixels.</summary>
    double offset;

    /// <summary>Gets or sets the Control that is scrolled. Setting it replaces the previous content.</summary>
    public Control? Content
    {
        get => Children.Count == 0 ? null : (Control)Children[0];
        set
        {
            Children.Clear();

            if (value is null)
                return;

            value.SetValue(FlexShrinkProperty, 0);

            Children.Add(value);
        }
    }

    /// <summary>Gets the distance the content is scrolled, in pixels. It is between 0 and <see cref="MaxOffset"/>.</summary>
    public double Offset => Math.Clamp(offset, 0, MaxOffset);

    /// <summary>Gets the largest <see cref="Offset"/>: how much taller the content is than the viewport. It is 0 when everything fits.</summary>
    /// <remarks>It is only meaningful after <see cref="Control.Arrange"/>.</remarks>
    public double MaxOffset => Math.Max(0, ContentExtent - Viewport.Height);

    /// <summary>Gets the height of the children, from the top of the first to the bottom of the last.</summary>
    public double ContentExtent => Children.Count == 0 ? 0 : Children.Max(c => ((Control)c).Bounds.Bottom) - Viewport.Y;

    /// <summary>Gets the visible area in the ScrollViewer's own coordinates.</summary>
    Rect Viewport => ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <inheritdoc/>
    protected override (double X, double Y) ChildOffset => (0, -Offset);

    /// <inheritdoc/>
    protected override Rect? ChildClip => Viewport;

    /// <inheritdoc/>
    /// <remarks>A ScrollViewer always lays its child out in a column, whatever <c>flex-direction</c> says.</remarks>
    protected override FlexDirection LayoutDirection => FlexDirection.Column;

    /// <inheritdoc/>
    public bool ScrollBy(double delta)
    {
        var before = Offset;

        offset = Math.Clamp(before + delta, 0, MaxOffset);

        return offset != before;
    }

    /// <summary>Scrolls to the top.</summary>
    /// <returns><see langword="true"/> when the offset changed.</returns>
    public bool ScrollToTop() => ScrollBy(-Offset);
}
