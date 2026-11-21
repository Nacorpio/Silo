using Silo.Styling.Values;

namespace Silo.Layout;

/// <summary>The direction of a flex container's main axis.</summary>
public enum FlexDirection
{
    /// <summary>Children are placed left to right. This is the default.</summary>
    Row,

    /// <summary>Children are placed top to bottom.</summary>
    Column,
}

/// <summary>How a flex container distributes free space along its main axis.</summary>
public enum JustifyContent
{
    /// <summary>Children are packed at the start. This is the default.</summary>
    Start,

    /// <summary>Children are packed in the middle.</summary>
    Center,

    /// <summary>Children are packed at the end.</summary>
    End,

    /// <summary>The first child is at the start, the last at the end, and the space is shared between children.</summary>
    SpaceBetween,

    /// <summary>The space is shared around every child, so the outer gaps are half the inner gaps.</summary>
    SpaceAround,

    /// <summary>The space is shared so that all gaps, including the outer ones, are equal.</summary>
    SpaceEvenly,
}

/// <summary>How a flex item is placed on the cross axis of its container.</summary>
public enum AlignItems
{
    /// <summary>The item is stretched to fill the cross axis when its cross size is not set. This is the default.</summary>
    Stretch,

    /// <summary>The item is placed at the cross start.</summary>
    Start,

    /// <summary>The item is centered on the cross axis.</summary>
    Center,

    /// <summary>The item is placed at the cross end.</summary>
    End,
}

/// <summary>
/// The Styled property values that the layout engine reads from a node.
/// </summary>
/// <remarks>
/// <para>
/// Sizes use the border-box model: <see cref="Width"/>, <see cref="Height"/>, their limits and <see cref="Basis"/> include the
/// node's padding and border, as if <c>box-sizing: border-box</c> was set everywhere.
/// </para>
/// <para>
/// A <see langword="null"/> size means <c>auto</c>. Percentages resolve against the content box of the parent, and
/// <see cref="LengthUnit.Em"/> and <see cref="LengthUnit.Rem"/> both resolve to 16 pixels per unit until text metrics are
/// available. <see cref="LengthUnit.Fr"/> is not valid here and is treated as <c>auto</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var style = new LayoutStyle
/// {
///     Direction = FlexDirection.Column,
///     Padding = new Thickness(new Length(8, LengthUnit.Px), new Length(8, LengthUnit.Px), new Length(8, LengthUnit.Px), new Length(8, LengthUnit.Px)),
///     Gap = new Length(4, LengthUnit.Px),
///     AlignItems = AlignItems.Center,
/// };
/// </code>
/// </example>
public sealed class LayoutStyle
{
    /// <summary>Gets or sets the main axis of the node's children. The default is <see cref="FlexDirection.Row"/>.</summary>
    public FlexDirection Direction { get; init; }

    /// <summary>Gets or sets how free main-axis space is distributed between children.</summary>
    public JustifyContent JustifyContent { get; init; }

    /// <summary>Gets or sets the default cross-axis placement of children. The default is <see cref="AlignItems.Stretch"/>.</summary>
    public AlignItems AlignItems { get; init; }

    /// <summary>Gets or sets this node's own cross-axis placement, overriding its parent's <see cref="AlignItems"/>. <see langword="null"/> uses the parent's.</summary>
    public AlignItems? AlignSelf { get; init; }

    /// <summary>Gets or sets the space between adjacent children along the main axis.</summary>
    public Length Gap { get; init; }

    /// <summary>Gets or sets the share of free main-axis space this node takes in its parent. The default is 0.</summary>
    public double Grow { get; init; }

    /// <summary>Gets or sets how strongly this node gives up space when its parent overflows. The default is 1.</summary>
    public double Shrink { get; init; } = 1;

    /// <summary>Gets or sets the starting main size in the parent. <see langword="null"/> falls back to the width or height, then to the content size.</summary>
    public Length? Basis { get; init; }

    /// <summary>Gets or sets the width, or <see langword="null"/> for auto.</summary>
    public Length? Width { get; init; }

    /// <summary>Gets or sets the height, or <see langword="null"/> for auto.</summary>
    public Length? Height { get; init; }

    /// <summary>Gets or sets the smallest width, or <see langword="null"/> for none.</summary>
    public Length? MinWidth { get; init; }

    /// <summary>Gets or sets the largest width, or <see langword="null"/> for none.</summary>
    public Length? MaxWidth { get; init; }

    /// <summary>Gets or sets the smallest height, or <see langword="null"/> for none.</summary>
    public Length? MinHeight { get; init; }

    /// <summary>Gets or sets the largest height, or <see langword="null"/> for none.</summary>
    public Length? MaxHeight { get; init; }

    /// <summary>Gets or sets the space between the node's border and its children.</summary>
    public Thickness Padding { get; init; }

    /// <summary>Gets or sets the width of the node's border on each side.</summary>
    public Thickness Border { get; init; }

    /// <summary>Gets or sets the space around the node, outside its border. Auto margins are not supported.</summary>
    public Thickness Margin { get; init; }
}
