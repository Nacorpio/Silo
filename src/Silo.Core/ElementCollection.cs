using System.Collections;

namespace Silo;

/// <summary>
/// The children of an <see cref="Element"/>. Adding a child sets its <see cref="Element.Parent"/>, and removing it clears it.
/// </summary>
public sealed class ElementCollection : IReadOnlyList<Element>
{
    /// <summary>The Element that owns the collection.</summary>
    readonly Element owner;

    /// <summary>The children in order.</summary>
    readonly List<Element> items = [];

    /// <summary>Creates the collection for an owner.</summary>
    /// <param name="owner">The Element whose children these are.</param>
    internal ElementCollection(Element owner) => this.owner = owner;

    /// <inheritdoc/>
    public int Count => items.Count;

    /// <inheritdoc/>
    public Element this[int index] => items[index];

    /// <summary>Appends a child.</summary>
    /// <param name="child">The Element to add.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="child"/> already has a parent, or adding it would make an Element its own ancestor.
    /// </exception>
    public void Add(Element child) => Insert(items.Count, child);

    /// <summary>Inserts a child at a position.</summary>
    /// <param name="index">The zero-based position of the new child.</param>
    /// <param name="child">The Element to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not between 0 and <see cref="Count"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="child"/> already has a parent, or inserting it would make an Element its own ancestor.
    /// </exception>
    public void Insert(int index, Element child)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)items.Count);

        if (child.Parent is not null)
        {
            throw new InvalidOperationException("The Element already has a parent. Remove it from there first.");
        }

        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
            {
                throw new InvalidOperationException("An Element cannot be added below itself.");
            }
        }

        items.Insert(index, child);

        child.SetParent(owner);
    }

    /// <summary>Removes a child.</summary>
    /// <param name="child">The Element to remove.</param>
    /// <returns><see langword="true"/> when the child was in the collection.</returns>
    public bool Remove(Element child)
    {
        if (!items.Remove(child))
        {
            return false;
        }

        child.SetParent(null);

        return true;
    }

    /// <inheritdoc/>
    public IEnumerator<Element> GetEnumerator() => items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
