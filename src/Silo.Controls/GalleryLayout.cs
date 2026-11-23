using Silo.Layout;

namespace Silo;

/// <summary>What a <see cref="GalleryLayout"/> is given besides the item sizes.</summary>
/// <param name="Width">The width of the area the items live in, in pixels.</param>
/// <param name="Height">The height of the area, in pixels.</param>
/// <param name="ItemWidth">The width of a cell, from <c>item-width</c>.</param>
/// <param name="ItemHeight">The height of a cell, or the target row height of a justified layout, from <c>item-height</c>.</param>
/// <param name="Gap">The space between items, from <c>gallery-gap</c>.</param>
/// <param name="Columns">The fixed column count, or 0 for as many as fit.</param>
public readonly record struct GalleryLayoutOptions(
    double Width, double Height, double ItemWidth, double ItemHeight, double Gap, int Columns);

/// <summary>Where a <see cref="GalleryLayout"/> put the items.</summary>
/// <param name="Rects">One rectangle per item, in the order of the sizes, with the origin at the top left of the content area.</param>
/// <param name="Extent">The size of everything, which is what the view scrolls over.</param>
public sealed record GalleryLayoutResult(IReadOnlyList<Rect> Rects, Size Extent);

/// <summary>
/// Decides where the items of a <see cref="GalleryView"/> go: item sizes in, rectangles out. The four built-in layouts are
/// <see cref="Grid"/>, <see cref="Justified"/>, <see cref="Masonry"/> and <see cref="Strip"/>.
/// </summary>
/// <remarks>
/// A layout only does arithmetic, so it can be tested without a window and a custom one is a small class. A size of 0 by 0
/// means the item has no natural size, and a layout then falls back to <see cref="GalleryLayoutOptions.ItemWidth"/> and
/// <see cref="GalleryLayoutOptions.ItemHeight"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// // Every item is a square, one per row.
/// sealed class Rows : GalleryLayout
/// {
///     public override GalleryLayoutResult Arrange(IReadOnlyList&lt;Size&gt; sizes, GalleryLayoutOptions options)
///     {
///         var rects = sizes.Select((_, i) => new Rect(0, i * (options.ItemHeight + options.Gap), options.Width, options.ItemHeight)).ToList();
///
///         return new GalleryLayoutResult(rects, new Size(options.Width, rects.Count * (options.ItemHeight + options.Gap)));
///     }
/// }
///
/// gallery.Layout = new Rows();
/// </code>
/// </example>
public abstract class GalleryLayout
{
    /// <summary>Gets equal cells that wrap onto new rows.</summary>
    public static GalleryLayout Grid { get; } = new GridLayout();

    /// <summary>Gets rows of equal height whose items are as wide as their aspect ratio makes them, filling each row.</summary>
    public static GalleryLayout Justified { get; } = new JustifiedLayout();

    /// <summary>Gets columns of equal width whose items are stacked under the shortest column by their aspect ratio.</summary>
    public static GalleryLayout Masonry { get; } = new MasonryLayout();

    /// <summary>Gets one horizontally scrolling row.</summary>
    public static GalleryLayout Strip { get; } = new StripLayout();

    /// <summary>Gets whether the view scrolls sideways. The default is <see langword="false"/>.</summary>
    public virtual bool IsHorizontal => false;

    /// <summary>Places the items.</summary>
    /// <param name="sizes">The natural size of each item.</param>
    /// <param name="options">The area and the styled values.</param>
    /// <returns>A rectangle for each item and the extent.</returns>
    public abstract GalleryLayoutResult Arrange(IReadOnlyList<Size> sizes, GalleryLayoutOptions options);

    /// <summary>Gets the width over height of a size, or 0 when it has none.</summary>
    /// <param name="size">The size.</param>
    /// <returns>The aspect ratio.</returns>
    protected static double Aspect(Size size) => size.Width > 0 && size.Height > 0 ? size.Width / size.Height : 0;

    /// <summary>Gets how many columns fit.</summary>
    /// <param name="options">The options.</param>
    /// <returns><see cref="GalleryLayoutOptions.Columns"/> when set, otherwise as many cells as fit, at least 1.</returns>
    protected static int ColumnCount(GalleryLayoutOptions options)
        => options.Columns > 0
            ? options.Columns
            : Math.Max(1, (int)Math.Floor((options.Width + options.Gap) / (Math.Max(1, options.ItemWidth) + options.Gap)));

    /// <summary>Gets the width of one of a number of columns that share the width.</summary>
    /// <param name="options">The options.</param>
    /// <param name="columns">The column count.</param>
    /// <returns>The width, which is the cell width when the columns were not fixed and there is room left over.</returns>
    protected static double ColumnWidth(GalleryLayoutOptions options, int columns)
        => options.Columns > 0
            ? Math.Max(1, (options.Width - options.Gap * (columns - 1)) / columns)
            : Math.Max(1, options.ItemWidth);

    sealed class GridLayout : GalleryLayout
    {
        public override GalleryLayoutResult Arrange(IReadOnlyList<Size> sizes, GalleryLayoutOptions options)
        {
            var columns = ColumnCount(options);
            var width = ColumnWidth(options, columns);
            var rects = new List<Rect>(sizes.Count);

            for (var i = 0; i < sizes.Count; i++)
                rects.Add(new Rect(i % columns * (width + options.Gap), i / columns * (options.ItemHeight + options.Gap), width, options.ItemHeight));

            var rows = (sizes.Count + columns - 1) / columns;
            var used = Math.Min(columns, sizes.Count);

            return new GalleryLayoutResult(rects, new Size(
                Math.Max(0, used * (width + options.Gap) - options.Gap),
                Math.Max(0, rows * (options.ItemHeight + options.Gap) - options.Gap)));
        }
    }

    sealed class JustifiedLayout : GalleryLayout
    {
        public override GalleryLayoutResult Arrange(IReadOnlyList<Size> sizes, GalleryLayoutOptions options)
        {
            var rects = new List<Rect>(sizes.Count);
            var row = new List<double>();
            var y = 0.0;

            for (var i = 0; i < sizes.Count; i++)
            {
                var aspect = Aspect(sizes[i]);

                row.Add(aspect > 0 ? aspect : Math.Max(1, options.ItemWidth) / Math.Max(1, options.ItemHeight));

                var natural = row.Sum() * options.ItemHeight + options.Gap * (row.Count - 1);

                if (natural < options.Width && i < sizes.Count - 1)
                    continue;

                var last = natural < options.Width;
                var scale = last ? 1 : (options.Width - options.Gap * (row.Count - 1)) / (row.Sum() * options.ItemHeight);
                var height = options.ItemHeight * scale;
                var x = 0.0;

                foreach (var ratio in row)
                {
                    rects.Add(new Rect(x, y, ratio * height, height));

                    x += ratio * height + options.Gap;
                }

                y += height + options.Gap;

                row.Clear();
            }

            return new GalleryLayoutResult(rects, new Size(options.Width, Math.Max(0, y - options.Gap)));
        }
    }

    sealed class MasonryLayout : GalleryLayout
    {
        public override GalleryLayoutResult Arrange(IReadOnlyList<Size> sizes, GalleryLayoutOptions options)
        {
            var columns = ColumnCount(options);
            var width = ColumnWidth(options, columns);
            var bottoms = new double[columns];
            var rects = new List<Rect>(sizes.Count);

            foreach (var size in sizes)
            {
                var shortest = Array.IndexOf(bottoms, bottoms.Min());
                var aspect = Aspect(size);
                var height = aspect > 0 ? width / aspect : options.ItemHeight;

                rects.Add(new Rect(shortest * (width + options.Gap), bottoms[shortest], width, height));

                bottoms[shortest] += height + options.Gap;
            }

            var extent = sizes.Count == 0 ? 0 : bottoms.Max() - options.Gap;

            return new GalleryLayoutResult(rects, new Size(Math.Max(0, columns * (width + options.Gap) - options.Gap), Math.Max(0, extent)));
        }
    }

    sealed class StripLayout : GalleryLayout
    {
        public override bool IsHorizontal => true;

        public override GalleryLayoutResult Arrange(IReadOnlyList<Size> sizes, GalleryLayoutOptions options)
        {
            var rects = new List<Rect>(sizes.Count);

            for (var i = 0; i < sizes.Count; i++)
                rects.Add(new Rect(i * (options.ItemWidth + options.Gap), 0, options.ItemWidth, options.ItemHeight));

            return new GalleryLayoutResult(rects, new Size(Math.Max(0, sizes.Count * (options.ItemWidth + options.Gap) - options.Gap), options.ItemHeight));
        }
    }
}
