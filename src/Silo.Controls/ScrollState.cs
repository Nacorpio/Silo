namespace Silo;

/// <summary>
/// The arithmetic of one scrolled axis: how far the content is scrolled, the furthest it can go, and how to keep a range in view.
/// </summary>
/// <remarks>
/// <para>
/// A Control that scrolls owns one ScrollState per axis and gives it two functions, one for the extent of the content and one
/// for the length of the viewport. They are read on every use, so the state never goes stale when items are added or the
/// Control is resized. All values are in pixels. The Control converts to its own units, such as rows, itself.
/// </para>
/// <para>
/// The stored offset is not clamped when the content shrinks, so a list that is emptied and filled again does not forget where
/// it was. <see cref="Offset"/> always reports the clamped value.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var scroll = new ScrollState(() => items.Count * rowHeight, () => viewport.Height);
///
/// scroll.ScrollBy(wheelDelta);
/// scroll.EnsureVisible(selected * rowHeight, rowHeight);
/// </code>
/// </example>
/// <param name="extent">Gives the length of the content.</param>
/// <param name="viewport">Gives the length of the visible area.</param>
public sealed class ScrollState(Func<double> extent, Func<double> viewport)
{
    /// <summary>The offset as it was last set, which may be beyond <see cref="MaxOffset"/> until it is read.</summary>
    double offset;

    /// <summary>Gets how far the content is scrolled, between 0 and <see cref="MaxOffset"/>.</summary>
    public double Offset => Math.Clamp(offset, 0, MaxOffset);

    /// <summary>Gets the furthest the content can be scrolled: how much longer it is than the viewport, or 0 when it fits.</summary>
    public double MaxOffset => Math.Max(0, extent() - viewport());

    /// <summary>Gets the length of the content.</summary>
    public double Extent => extent();

    /// <summary>Gets the length of the visible area.</summary>
    public double Viewport => viewport();

    /// <summary>Scrolls by a distance, stopping at the ends.</summary>
    /// <param name="delta">The distance. Positive scrolls towards the end.</param>
    /// <returns><see langword="true"/> when the offset changed, so a new frame is needed.</returns>
    public bool ScrollBy(double delta)
    {
        var before = Offset;

        offset = Math.Clamp(before + delta, 0, MaxOffset);

        return offset != before;
    }

    /// <summary>Scrolls to an offset, which is limited to the range that is possible.</summary>
    /// <param name="value">The offset.</param>
    /// <returns><see langword="true"/> when the offset changed.</returns>
    public bool ScrollTo(double value) => ScrollBy(value - Offset);

    /// <summary>Scrolls just enough to show a range of the content.</summary>
    /// <param name="start">Where the range starts, from the start of the content.</param>
    /// <param name="length">The length of the range.</param>
    /// <remarks>It does nothing while the viewport has no length. A range longer than the viewport shows its end.</remarks>
    public void EnsureVisible(double start, double length)
    {
        var size = viewport();

        if (size <= 0)
            return;

        if (start < offset)
        {
            offset = start;

            return;
        }

        offset = Math.Max(offset, start + length - size);
    }

    /// <summary>Applies the limits to the stored offset, for a Control that lays out after its content changed.</summary>
    public void Clamp() => offset = Offset;

    /// <summary>Scrolls back to the start.</summary>
    public void Reset() => offset = 0;
}
