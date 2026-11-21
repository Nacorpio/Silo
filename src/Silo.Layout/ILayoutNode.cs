namespace Silo.Layout;

/// <summary>
/// A node that the layout engine can size and position.
/// </summary>
/// <remarks>
/// Implement it on an Element wrapper or on a plain test node. The engine reads <see cref="Style"/> and
/// <see cref="Children"/>, calls <see cref="Measure"/> on nodes without children, and reports the result through
/// <see cref="SetLayout"/>. It never keeps a reference to a node after a call returns.
/// </remarks>
public interface ILayoutNode
{
    /// <summary>Gets the layout properties of the node.</summary>
    LayoutStyle Style { get; }

    /// <summary>Gets the children of the node, in order. A node without children is a leaf.</summary>
    IReadOnlyList<ILayoutNode> Children { get; }

    /// <summary>
    /// Measures the content of a leaf, such as the text of a TextBlock. Padding and border are not part of the result.
    /// </summary>
    /// <param name="availableWidth">
    /// The width the content may use, or <see langword="null"/> when it should report its natural, unwrapped width.
    /// </param>
    /// <returns>The size the content needs. For text that wraps, the height depends on <paramref name="availableWidth"/>.</returns>
    Size Measure(double? availableWidth);

    /// <summary>Receives the final border box of the node, relative to its parent's border box.</summary>
    /// <param name="rect">The position and size assigned by layout.</param>
    void SetLayout(Rect rect);
}
