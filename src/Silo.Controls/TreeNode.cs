using Silo.Rendering;

namespace Silo;

/// <summary>One node of a <see cref="TreeView"/>: a line of text with an optional icon and child nodes.</summary>
/// <remarks>
/// <para>
/// Build the tree with <see cref="Add"/> and give it to a <see cref="TreeView"/> with <see cref="TreeView.Add"/>. Once a node is
/// in a TreeView, changing its text, icon, children or <see cref="IsExpanded"/> updates the view. A node can only be in one
/// place of one tree.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var src = new TreeNode("src") { IsExpanded = true };
///
/// src.Add(new TreeNode("Program.cs") { Tag = programFile });
/// tree.Add(src);
/// </code>
/// </example>
public sealed class TreeNode
{
    /// <summary>The children.</summary>
    readonly List<TreeNode> children = [];

    /// <summary>The text.</summary>
    string text;

    /// <summary>The icon.</summary>
    IImage? icon;

    /// <summary>Whether the children are shown.</summary>
    bool isExpanded;

    /// <summary>The node that this node is a child of, which is the invisible root of a TreeView for a top-level node.</summary>
    TreeNode? parent;

    /// <summary>Whether this is the invisible root of a TreeView.</summary>
    bool isRoot;

    /// <summary>Creates a node.</summary>
    /// <param name="text">The text.</param>
    /// <param name="icon">A small picture shown in front of the text, or <see langword="null"/> for none.</param>
    public TreeNode(string text, IImage? icon = null)
    {
        this.text = text ?? string.Empty;
        this.icon = icon;
    }

    /// <summary>Creates the invisible node that holds the top-level nodes of a TreeView.</summary>
    /// <param name="owner">The TreeView.</param>
    internal TreeNode(TreeView owner)
    {
        text = string.Empty;
        isRoot = true;
        isExpanded = true;
        Owner = owner;
    }

    /// <summary>Gets or sets the text.</summary>
    public string Text
    {
        get => text;
        set
        {
            text = value ?? string.Empty;

            Owner?.Rebuild();
        }
    }

    /// <summary>Gets or sets the icon, or <see langword="null"/> for none.</summary>
    public IImage? Icon
    {
        get => icon;
        set
        {
            icon = value;

            Owner?.Rebuild();
        }
    }

    /// <summary>Gets or sets something that belongs to the node, such as the item that it shows.</summary>
    public object? Tag { get; set; }

    /// <summary>Gets the node that this node is a child of, or <see langword="null"/> for a top-level node.</summary>
    public TreeNode? Parent => parent is { isRoot: false } ? parent : null;

    /// <summary>Gets the children, in order.</summary>
    public IReadOnlyList<TreeNode> Children => children;

    /// <summary>Gets the number of ancestors, which is 0 for a top-level node.</summary>
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>Gets or sets whether the children are shown. It has no effect on a node without children.</summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
                return;

            isExpanded = value;

            Owner?.OnNodeExpandedChanged(this);
        }
    }

    /// <summary>Gets the TreeView that the node is in, or <see langword="null"/>.</summary>
    internal TreeView? Owner { get; private set; }

    /// <summary>Adds a child at the end.</summary>
    /// <param name="child">The node to add. It must not be in a tree yet.</param>
    /// <returns>The child, so that it can be filled in right away.</returns>
    /// <exception cref="InvalidOperationException">The node is in a tree already.</exception>
    public TreeNode Add(TreeNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.parent is not null || child.Owner is not null || child == this)
            throw new InvalidOperationException("The node is already part of a tree.");

        child.parent = this;
        children.Add(child);
        child.Attach(Owner);

        Owner?.Rebuild();

        return child;
    }

    /// <summary>Removes a child.</summary>
    /// <param name="child">The child.</param>
    /// <returns><see langword="true"/> when it was a child of this node.</returns>
    public bool Remove(TreeNode child)
    {
        if (!children.Remove(child))
            return false;

        child.parent = null;

        var owner = Owner;

        child.Attach(null);

        owner?.NodeRemoved(child);

        return true;
    }

    /// <summary>Tells whether a node is below this node.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when this node is an ancestor of it.</returns>
    public bool Contains(TreeNode node)
    {
        for (var n = node.parent; n is not null; n = n.parent)
            if (n == this)
                return true;

        return false;
    }

    /// <summary>Sets the TreeView of this node and its descendants.</summary>
    /// <param name="owner">The TreeView, or <see langword="null"/> when the node was removed.</param>
    void Attach(TreeView? owner)
    {
        Owner = owner;

        foreach (var child in children)
            child.Attach(owner);
    }
}
