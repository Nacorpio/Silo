using Silo.Styling.Values;

namespace Silo.Layout;

/// <summary>
/// A single-line flexbox layout engine for trees of <see cref="ILayoutNode"/>.
/// </summary>
/// <remarks>
/// <para>
/// It implements the parts of CSS flexbox that Milestone 1 needs: row and column direction, <c>gap</c>, <c>flex-grow</c>,
/// <c>flex-shrink</c>, <c>flex-basis</c>, <c>justify-content</c>, <c>align-items</c>, <c>align-self</c>, min and max sizes,
/// padding, border and margin. Wrapping, reverse directions, auto margins and grid are not implemented yet.
/// </para>
/// <para>
/// Layout is a pure function of the tree and the available size, so it runs without a window and can be tested against
/// fixtures.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// // A row of two items where the second one takes the remaining width.
/// ILayoutNode root = new MyNode(new LayoutStyle { Gap = new Length(8, LengthUnit.Px) },
///     new MyNode(new LayoutStyle { Width = new Length(100, LengthUnit.Px) }),
///     new MyNode(new LayoutStyle { Grow = 1 }));
///
/// FlexLayout.Arrange(root, new Size(400, 300));
/// </code>
/// </example>
public static class FlexLayout
{
    /// <summary>The number of pixels that one <c>em</c> or <c>rem</c> resolves to until text metrics are available.</summary>
    const double EmPixels = 16;

    /// <summary>
    /// Lays out a tree and reports the result to every node through <see cref="ILayoutNode.SetLayout"/>.
    /// </summary>
    /// <param name="root">The root of the tree. Its position is 0, 0.</param>
    /// <param name="available">
    /// The size of the area the root lives in, usually the window's client area. A root without a width or height fills the area.
    /// </param>
    public static void Arrange(ILayoutNode root, Size available)
    {
        ArgumentNullException.ThrowIfNull(root);

        var style = root.Style;

        var width = Clamp(Resolve(style.Width, available.Width) ?? available.Width, style.MinWidth, style.MaxWidth, available.Width);
        var height = Clamp(Resolve(style.Height, available.Height) ?? available.Height, style.MinHeight, style.MaxHeight, available.Height);

        Place(root, new Rect(0, 0, Math.Max(0, width), Math.Max(0, height)));
    }

    /// <summary>One child of a container while its main size is being resolved.</summary>
    sealed class Item
    {
        /// <summary>The child node.</summary>
        public required ILayoutNode Node { get; init; }


        /// <summary>The margin before the child on the main axis.</summary>
        public double MainMarginStart;

        /// <summary>The margin after the child on the main axis.</summary>
        public double MainMarginEnd;

        /// <summary>The margin before the child on the cross axis.</summary>
        public double CrossMarginStart;

        /// <summary>The margin after the child on the cross axis.</summary>
        public double CrossMarginEnd;

        /// <summary>The padding plus border along the main axis, which is the smallest the child can be.</summary>
        public double MainInsets;

        /// <summary>The padding plus border along the cross axis.</summary>
        public double CrossInsets;

        /// <summary>The flex base size, before growing or shrinking.</summary>
        public double Base;

        /// <summary>The base size limited by the child's min and max.</summary>
        public double Hypothetical;

        /// <summary>The smallest main size.</summary>
        public double Min;

        /// <summary>The largest main size.</summary>
        public double Max = double.PositiveInfinity;

        /// <summary>The resolved main size.</summary>
        public double Target;

        /// <summary>Whether <see cref="Target"/> is final.</summary>
        public bool Frozen;


        /// <summary>Gets the sum of both main margins.</summary>
        public double MainMargins => MainMarginStart + MainMarginEnd;

        /// <summary>Gets the sum of both cross margins.</summary>
        public double CrossMargins => CrossMarginStart + CrossMarginEnd;
    }

    /// <summary>Assigns a border box to a node and lays out its children inside it.</summary>
    /// <param name="node">The node to place.</param>
    /// <param name="rect">The border box, relative to the parent's border box.</param>
    static void Place(ILayoutNode node, Rect rect)
    {
        node.SetLayout(rect);

        if (node.Children.Count == 0)
            return;

        var style = node.Style;
        var isRow = style.Direction == FlexDirection.Row;

        var top = Inset(style.Padding.Top, rect.Width) + Inset(style.Border.Top, rect.Width);
        var right = Inset(style.Padding.Right, rect.Width) + Inset(style.Border.Right, rect.Width);
        var bottom = Inset(style.Padding.Bottom, rect.Width) + Inset(style.Border.Bottom, rect.Width);
        var left = Inset(style.Padding.Left, rect.Width) + Inset(style.Border.Left, rect.Width);

        var contentWidth = Math.Max(0, rect.Width - left - right);
        var contentHeight = Math.Max(0, rect.Height - top - bottom);

        var mainSize = isRow ? contentWidth : contentHeight;
        var crossSize = isRow ? contentHeight : contentWidth;

        var gap = Inset(style.Gap, mainSize);

        var items = new List<Item>(node.Children.Count);

        foreach (var child in node.Children)
            items.Add(CreateItem(child, isRow, contentWidth, mainSize, crossSize));

        ResolveFlexibleLengths(items, mainSize, gap);

        var used = items.Sum(i => i.Target + i.MainMargins) + gap * (items.Count - 1);
        var free = Math.Max(0, mainSize - used);

        var (offset, between) = Distribute(style.JustifyContent, free, items.Count);

        foreach (var item in items)
        {
            var child = item.Node.Style;
            var align = child.AlignSelf ?? style.AlignItems;

            var crossMin = isRow ? child.MinHeight : child.MinWidth;
            var crossMax = isRow ? child.MaxHeight : child.MaxWidth;

            var cross = CrossSize(item, isRow, align, crossSize);

            cross = Math.Max(item.CrossInsets, Clamp(cross, crossMin, crossMax, crossSize));

            var crossOffset = align switch
            {
                AlignItems.Center => (crossSize - cross - item.CrossMargins) / 2 + item.CrossMarginStart,
                AlignItems.End => crossSize - cross - item.CrossMarginEnd,

                _ => item.CrossMarginStart,
            };

            offset += item.MainMarginStart;

            var main = offset;

            offset += item.Target + item.MainMarginEnd + gap + between;

            var childRect = isRow
                ? new Rect(left + main, top + crossOffset, item.Target, cross)
                : new Rect(left + crossOffset, top + main, cross, item.Target);

            Place(item.Node, childRect);
        }
    }

    /// <summary>Resolves a child's margins, limits and flex base size.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="isRow">Whether the container's main axis is horizontal.</param>
    /// <param name="contentWidth">The container's content width, which percentage margins and insets resolve against.</param>
    /// <param name="mainSize">The container's content size along the main axis.</param>
    /// <param name="crossSize">The container's content size along the cross axis.</param>
    /// <returns>The child's item, with <see cref="Item.Hypothetical"/> set.</returns>
    static Item CreateItem(ILayoutNode child, bool isRow, double contentWidth, double mainSize, double crossSize)
    {
        var s = child.Style;

        var item = new Item
        {
            Node = child,
            MainMarginStart = Inset(isRow ? s.Margin.Left : s.Margin.Top, contentWidth),
            MainMarginEnd = Inset(isRow ? s.Margin.Right : s.Margin.Bottom, contentWidth),
            CrossMarginStart = Inset(isRow ? s.Margin.Top : s.Margin.Left, contentWidth),
            CrossMarginEnd = Inset(isRow ? s.Margin.Bottom : s.Margin.Right, contentWidth),
            MainInsets = Insets(s, isRow, contentWidth),
            CrossInsets = Insets(s, !isRow, contentWidth),
        };

        var mainProperty = isRow ? s.Width : s.Height;
        var crossProperty = isRow ? s.Height : s.Width;
        var min = isRow ? s.MinWidth : s.MinHeight;
        var max = isRow ? s.MaxWidth : s.MaxHeight;

        item.Min = Math.Max(item.MainInsets, Resolve(min, mainSize) ?? 0);

        if (Resolve(max, mainSize) is { } maximum)
            item.Max = Math.Max(item.Min, maximum);

        item.Base = BaseSize(item, isRow, s.Basis ?? mainProperty, crossProperty, mainSize, crossSize);

        item.Hypothetical = Math.Clamp(item.Base, item.Min, item.Max);

        return item;
    }

    /// <summary>
    /// Grows or shrinks the items to fill the main axis, following the CSS flexbox algorithm for a single line.
    /// </summary>
    /// <param name="items">The items, whose <see cref="Item.Target"/> is set on return.</param>
    /// <param name="mainSize">The container's content size along the main axis.</param>
    /// <param name="gap">The space between adjacent items.</param>
    static void ResolveFlexibleLengths(List<Item> items, double mainSize, double gap)
    {
        var gaps = gap * Math.Max(0, items.Count - 1);
        var hypotheticalTotal = items.Sum(i => i.Hypothetical + i.MainMargins) + gaps;
        var growing = hypotheticalTotal < mainSize;

        foreach (var item in items)
        {
            var factor = growing ? item.Node.Style.Grow : item.Node.Style.Shrink;

            item.Target = item.Hypothetical;
            item.Frozen = factor <= 0 || (growing ? item.Base > item.Hypothetical : item.Base < item.Hypothetical);
        }

        Action<List<Item>, double> distribute = growing ? Grow : Shrink;

        while (items.Any(i => !i.Frozen))
        {
            var taken = items.Sum(i => (i.Frozen ? i.Target : i.Base) + i.MainMargins) + gaps;
            var free = mainSize - taken;

            var unfrozen = items.Where(i => !i.Frozen).ToList();

            distribute(unfrozen, free);

            var violation = 0.0;

            foreach (var item in unfrozen)
            {
                var clamped = Math.Clamp(item.Target, item.Min, item.Max);

                violation += clamped - item.Target;

                item.Target = clamped;
            }

            foreach (var item in unfrozen)
            {
                var freeze = violation == 0 || (violation > 0 ? item.Target <= item.Min : item.Target >= item.Max);

                item.Frozen |= freeze;
            }
        }
    }

    /// <summary>Computes the flex base size of an item.</summary>
    /// <param name="item">The item, with margins and insets set.</param>
    /// <param name="isRow">Whether the container's main axis is horizontal.</param>
    /// <param name="basis">The flex basis or main size property, or <see langword="null"/> for content size.</param>
    /// <param name="crossProperty">The item's size property on the cross axis, or <see langword="null"/>.</param>
    /// <param name="mainSize">The container's content size along the main axis.</param>
    /// <param name="crossSize">The container's content size along the cross axis.</param>
    /// <returns>The base size in pixels.</returns>
    static double BaseSize(Item item, bool isRow, Length? basis, Length? crossProperty, double mainSize, double crossSize)
    {
        if (Resolve(basis, mainSize) is { } resolved)
            return Math.Max(item.MainInsets, resolved);

        if (isRow)
            return Intrinsic(item.Node, null).Width;

        var limit = Resolve(crossProperty, crossSize) ?? (crossSize - item.CrossMargins);

        return Intrinsic(item.Node, limit).Height;
    }

    /// <summary>Computes an item's border box size on the cross axis, before its min and max apply.</summary>
    /// <param name="item">The item, whose main size is already resolved.</param>
    /// <param name="isRow">Whether the container's main axis is horizontal.</param>
    /// <param name="align">The item's effective alignment.</param>
    /// <param name="crossSize">The container's content size along the cross axis.</param>
    /// <returns>The cross size in pixels.</returns>
    static double CrossSize(Item item, bool isRow, AlignItems align, double crossSize)
    {
        var style = item.Node.Style;

        if (Resolve(isRow ? style.Height : style.Width, crossSize) is { } explicitCross)
            return explicitCross;

        if (align == AlignItems.Stretch)
            return crossSize - item.CrossMargins;

        if (isRow)
            return Intrinsic(item.Node, item.Target).Height;

        return Intrinsic(item.Node, crossSize - item.CrossMargins).Width;
    }

    /// <summary>Hands out free space to growing items in proportion to their grow factors.</summary>
    /// <param name="items">The items that are not frozen yet.</param>
    /// <param name="free">The free space, which is positive when growing.</param>
    static void Grow(List<Item> items, double free)
    {
        var total = items.Sum(i => i.Node.Style.Grow);

        if (total < 1)
            free *= total;

        foreach (var item in items)
            item.Target = item.Base + Share(free, item.Node.Style.Grow, total);
    }

    /// <summary>Takes overflow back from shrinking items in proportion to their shrink factor times their base size.</summary>
    /// <param name="items">The items that are not frozen yet.</param>
    /// <param name="free">The free space, which is negative when shrinking.</param>
    static void Shrink(List<Item> items, double free)
    {
        var total = items.Sum(i => i.Node.Style.Shrink * i.Base);

        foreach (var item in items)
            item.Target = item.Base + Share(free, item.Node.Style.Shrink * item.Base, total);
    }

    /// <summary>Computes one item's part of the free space.</summary>
    /// <param name="free">The free space to share out.</param>
    /// <param name="weight">The item's weight, a grow factor or a shrink factor times the base size.</param>
    /// <param name="total">The sum of the weights of all items taking part.</param>
    /// <returns>The item's share, or 0 when nothing is shared.</returns>
    static double Share(double free, double weight, double total) => total > 0 ? free * weight / total : 0;

    /// <summary>Computes the starting offset and the extra space between items for a <see cref="JustifyContent"/>.</summary>
    /// <param name="justify">The distribution mode.</param>
    /// <param name="free">The free space on the main axis, never negative.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>The offset of the first item and the extra space added after each item.</returns>
    static (double Offset, double Between) Distribute(JustifyContent justify, double free, int count)
    {
        return justify switch
        {
            JustifyContent.Center => (free / 2, 0),
            JustifyContent.End => (free, 0),
            JustifyContent.SpaceBetween when count > 1 => (0, free / (count - 1)),
            JustifyContent.SpaceAround when count > 0 => (free / count / 2, free / count),
            JustifyContent.SpaceEvenly when count > 0 => (free / (count + 1), free / (count + 1)),

            _ => (0, 0),
        };
    }

    /// <summary>
    /// Computes the size a node wants when its width, height and flex properties are ignored.
    /// </summary>
    /// <param name="node">The node to measure.</param>
    /// <param name="availableWidth">The width its content may use, border box included, or <see langword="null"/> for natural width.</param>
    /// <returns>The border box size that fits the content.</returns>
    static Size Intrinsic(ILayoutNode node, double? availableWidth)
    {
        var style = node.Style;
        var basis = availableWidth ?? 0;

        var horizontal = Insets(style, true, basis);
        var vertical = Insets(style, false, basis);

        if (node.Children.Count == 0)
        {
            var content = node.Measure(availableWidth is null ? null : Math.Max(0, availableWidth.Value - horizontal));

            return new Size(content.Width + horizontal, content.Height + vertical);
        }

        var isRow = style.Direction == FlexDirection.Row;
        var contentLimit = availableWidth is null ? (double?)null : Math.Max(0, availableWidth.Value - horizontal);

        var gap = Inset(style.Gap, contentLimit ?? 0) * (node.Children.Count - 1);

        double main = gap;
        double cross = 0;

        foreach (var child in node.Children)
        {
            var outer = OuterSize(child, contentLimit);

            main += isRow ? outer.Width : outer.Height;
            cross = Math.Max(cross, isRow ? outer.Height : outer.Width);
        }

        return isRow
            ? new Size(main + horizontal, cross + vertical)
            : new Size(cross + horizontal, main + vertical);
    }

    /// <summary>Computes the size of a child including its margins, using its own width and height when it has them.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="availableWidth">The content width of the parent, or <see langword="null"/> for natural width.</param>
    /// <returns>The size of the margin box.</returns>
    static Size OuterSize(ILayoutNode child, double? availableWidth)
    {
        var style = child.Style;
        var basis = availableWidth ?? 0;

        var marginX = Inset(style.Margin.Left, basis) + Inset(style.Margin.Right, basis);
        var marginY = Inset(style.Margin.Top, basis) + Inset(style.Margin.Bottom, basis);

        var width = Resolve(style.Width, basis);
        var height = Resolve(style.Height, basis);

        var inner = width is null || height is null
            ? Intrinsic(child, width ?? (availableWidth is null ? null : availableWidth - marginX))
            : default;

        var w = Clamp(width ?? inner.Width, style.MinWidth, style.MaxWidth, basis);
        var h = Clamp(height ?? inner.Height, style.MinHeight, style.MaxHeight, basis);

        return new Size(w + marginX, h + marginY);
    }

    /// <summary>Sums padding and border on one axis.</summary>
    /// <param name="style">The node's style.</param>
    /// <param name="horizontal"><see langword="true"/> for left and right, <see langword="false"/> for top and bottom.</param>
    /// <param name="basis">The width that percentages resolve against.</param>
    /// <returns>The combined insets in pixels.</returns>
    static double Insets(LayoutStyle style, bool horizontal, double basis)
    {
        if (horizontal)
            return Horizontal(style.Padding, basis) + Horizontal(style.Border, basis);

        return Vertical(style.Padding, basis) + Vertical(style.Border, basis);
    }

    /// <summary>Sums the left and right sides of a thickness.</summary>
    /// <param name="thickness">The thickness.</param>
    /// <param name="basis">The width that percentages resolve against.</param>
    /// <returns>The combined width in pixels.</returns>
    static double Horizontal(Thickness thickness, double basis) => Inset(thickness.Left, basis) + Inset(thickness.Right, basis);

    /// <summary>Sums the top and bottom sides of a thickness.</summary>
    /// <param name="thickness">The thickness.</param>
    /// <param name="basis">The width that percentages resolve against.</param>
    /// <returns>The combined height in pixels.</returns>
    static double Vertical(Thickness thickness, double basis) => Inset(thickness.Top, basis) + Inset(thickness.Bottom, basis);

    /// <summary>Resolves a length that cannot be auto, such as padding, to pixels. Negative results become 0.</summary>
    /// <param name="length">The length.</param>
    /// <param name="basis">The value that 100% stands for.</param>
    /// <returns>The size in pixels.</returns>
    static double Inset(Length length, double basis) => Math.Max(0, Resolve(length, basis) ?? 0);

    /// <summary>Resolves an optional length to pixels.</summary>
    /// <param name="length">The length, or <see langword="null"/> for auto.</param>
    /// <param name="basis">The value that 100% stands for.</param>
    /// <returns>The size in pixels, or <see langword="null"/> for auto and for units that are not valid in layout.</returns>
    static double? Resolve(Length? length, double basis)
    {
        return length switch
        {
            null => null,
            { Unit: LengthUnit.Px } l => l.Value,
            { Unit: LengthUnit.Percent } l => basis * l.Value / 100,
            { Unit: LengthUnit.Em or LengthUnit.Rem } l => l.Value * EmPixels,

            _ => null,
        };
    }

    /// <summary>Limits a size by optional minimum and maximum lengths, where the minimum wins.</summary>
    /// <param name="value">The size to limit.</param>
    /// <param name="min">The minimum, or <see langword="null"/>.</param>
    /// <param name="max">The maximum, or <see langword="null"/>.</param>
    /// <param name="basis">The value that 100% stands for.</param>
    /// <returns>The limited size.</returns>
    static double Clamp(double value, Length? min, Length? max, double basis)
    {
        if (Resolve(max, basis) is { } maximum)
            value = Math.Min(value, maximum);

        if (Resolve(min, basis) is { } minimum)
            value = Math.Max(value, minimum);

        return value;
    }
}
