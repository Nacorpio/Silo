using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>A scrolling tree of <see cref="TreeNode"/>s, in which a node with children can be expanded and collapsed.</summary>
/// <remarks>
/// <para>
/// A TreeView is a <see cref="ListView"/> of the nodes that are visible, so it scrolls, highlights and styles the same way,
/// and <see cref="ListView.SelectedIndex"/> is the index among the visible rows. Prefer <see cref="SelectedNode"/>. Every row has
/// the height <c>item-height</c>, and each level moves its rows right by <c>indent</c> (16px by default). Give a TreeView a
/// <c>height</c>, as for a ListView.
/// </para>
/// <para>
/// A press on the triangle in front of a node expands or collapses it. With the keyboard, Right expands a collapsed node or moves
/// to its first child, Left collapses an expanded node or moves to its parent, Enter expands or collapses a node with children and
/// raises <see cref="NodeActivated"/> for a node without, and Up, Down, Home and End move through the visible rows.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tree = new TreeView { Name = "files" };
/// var src = tree.Add(new TreeNode("src") { IsExpanded = true });
///
/// src.Add(new TreeNode("Program.cs"));
/// tree.SelectedNodeChanged += (_, node) => Show(node?.Tag);
/// </code>
/// </example>
public class TreeView : ListView
{
    /// <summary>The width of the triangle area in front of a node in pixels.</summary>
    const double ChevronSpace = 16;

    /// <summary>The size of one step of the triangle in pixels.</summary>
    const double ChevronStep = 2;


    /// <summary>How far each level moves its rows right, in pixels.</summary>
    public static readonly StyledProperty<double> IndentProperty =
        StyledProperty.Register<TreeView, double>("indent", 16, ValueParsers.Pixels);


    /// <summary>The invisible node that holds the top-level nodes.</summary>
    readonly TreeNode root;

    /// <summary>The nodes that are visible, in the order of the rows.</summary>
    readonly List<TreeNode> visible = [];

    /// <summary>The selected node.</summary>
    TreeNode? selected;

    /// <summary>Whether every node is being expanded or collapsed, so that single changes are not reported.</summary>
    bool bulk;

    /// <summary>Whether the rows are being rebuilt, so that the selection events they cause are ignored.</summary>
    bool rebuilding;


    /// <summary>
    /// Runs before the first TreeView is created, so that the Styled property above is registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TreeView()
    {
    }

    /// <summary>Creates an empty TreeView.</summary>
    public TreeView()
    {
        root = new TreeNode(this);

        SelectionChanged += (_, _) => OnRowSelected();
    }


    /// <summary>Raised after <see cref="SelectedNode"/> changed. The argument is the new node, or <see langword="null"/>.</summary>
    public event EventHandler<TreeNode?>? SelectedNodeChanged;

    /// <summary>Raised after a node was expanded or collapsed, with that node.</summary>
    public event EventHandler<TreeNode>? NodeExpandedChanged;

    /// <summary>Raised when Enter is pressed on a node that has no children, with that node.</summary>
    public event EventHandler<TreeNode>? NodeActivated;


    /// <summary>Gets the top-level nodes, in order.</summary>
    public IReadOnlyList<TreeNode> Nodes => root.Children;

    /// <summary>Gets or sets the selected node, or <see langword="null"/> for none. A node that is hidden by a collapsed ancestor is expanded to.</summary>
    public TreeNode? SelectedNode
    {
        get => selected;
        set
        {
            if (value is not null && value.Owner != this)
                throw new ArgumentException("The node is not in this tree.", nameof(value));

            for (var n = value?.Parent; n is not null; n = n.Parent)
                n.IsExpanded = true;

            SelectedIndex = value is null ? -1 : visible.IndexOf(value);
        }
    }

    /// <summary>Gets how far each level moves its rows right, in pixels.</summary>
    double Indent => Math.Max(0, GetValue(IndentProperty));


    /// <summary>Adds a top-level node at the end.</summary>
    /// <param name="node">The node. It must not be in a tree yet.</param>
    /// <returns>The node, so that it can be filled in right away.</returns>
    public TreeNode Add(TreeNode node) => root.Add(node);

    /// <summary>Removes a top-level node.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when it was a top-level node of this tree.</returns>
    public bool Remove(TreeNode node) => root.Remove(node);

    /// <summary>Expands every node that has children.</summary>
    public void ExpandAll() => SetAll(true);

    /// <summary>Collapses every node. A selected node below another node is replaced by the top-level node that holds it.</summary>
    public void CollapseAll() => SetAll(false);

    /// <inheritdoc/>
    public override bool PointerDown(PointerEvent e)
    {
        var toggled = ToggleChevron(e);

        return base.PointerDown(e) | toggled;
    }

    /// <summary>Expands or collapses the node whose chevron is under the pointer.</summary>
    /// <param name="e">The pointer position relative to the TreeView's border box.</param>
    /// <returns><see langword="true"/> when a node was expanded or collapsed.</returns>
    bool ToggleChevron(PointerEvent e)
    {
        var row = (int)Math.Floor((e.Y - Viewport.Y + Offset) / RowHeight);

        if (row < 0 || row >= visible.Count || visible[row].Children.Count == 0)
            return false;

        var left = ChevronLeft(row);

        if (e.X < left || e.X >= left + ChevronSpace)
            return false;

        visible[row].IsExpanded = !visible[row].IsExpanded;

        return true;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var node = selected;

        if (node is null)
            return base.KeyDown(key);

        switch (key)
        {
            case Key.Right when node.Children.Count > 0 && !node.IsExpanded:
                node.IsExpanded = true;

                return true;

            case Key.Right when node.Children.Count > 0:
                SelectedNode = node.Children[0];

                return true;

            case Key.Left when node.IsExpanded && node.Children.Count > 0:
                node.IsExpanded = false;

                return true;

            case Key.Left when node.Parent is { } parent:
                SelectedNode = parent;

                return true;

            case Key.Enter when node.Children.Count > 0:
                node.IsExpanded = !node.IsExpanded;

                return true;

            case Key.Enter:
                NodeActivated?.Invoke(this, node);

                return false;
        }

        return base.KeyDown(key);
    }

    /// <summary>Rebuilds the rows from the nodes. Nodes call it when they change, so it is rarely needed.</summary>
    internal void Rebuild()
    {
        var offset = Offset;

        rebuilding = true;

        visible.Clear();

        Collect(root);

        Items = visible.Select(node => node.Text).ToList();
        Icons = visible.Select(node => node.Icon).ToList();

        ScrollBy(offset);

        if (selected is not null && !visible.Contains(selected))
            selected = null;

        SelectedIndex = selected is null ? -1 : visible.IndexOf(selected);

        rebuilding = false;
    }

    /// <summary>Runs when a node expanded or collapsed.</summary>
    /// <param name="node">The node.</param>
    internal void OnNodeExpandedChanged(TreeNode node)
    {
        if (bulk)
            return;

        var moved = !node.IsExpanded && selected is not null && node.Contains(selected);

        if (moved)
            selected = node;

        Rebuild();

        if (moved)
            SelectedNodeChanged?.Invoke(this, node);

        NodeExpandedChanged?.Invoke(this, node);
    }

    /// <summary>Runs when a node was removed from the tree.</summary>
    /// <param name="node">The node and everything below it.</param>
    internal void NodeRemoved(TreeNode node)
    {
        var lost = selected is not null && (selected == node || node.Contains(selected));

        Rebuild();

        if (lost)
            SelectedNodeChanged?.Invoke(this, null);
    }

    /// <summary>Expands or collapses every node and rebuilds the rows once.</summary>
    /// <param name="expanded">The new state.</param>
    void SetAll(bool expanded)
    {
        bulk = true;

        Walk(root, node => node.IsExpanded = expanded);

        bulk = false;

        var moved = !expanded && selected is { Parent: not null };

        if (moved)
            selected = Top(selected!);

        Rebuild();

        if (moved)
            SelectedNodeChanged?.Invoke(this, selected);
    }

    /// <inheritdoc/>
    protected override double RowIndent(int index) => (visible[index].Depth * Indent) + ChevronSpace;

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);

        context.Save();
        context.ClipRect(content);

        for (var i = 0; i < visible.Count; i++)
            if (visible[i].Children.Count > 0)
                DrawChevron(context, i);

        context.Restore();
    }

    /// <summary>Draws the triangle in front of a node: right while it is collapsed and down while it is expanded.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="row">The row's index.</param>
    void DrawChevron(IDrawingContext context, int row)
    {
        var top = RowTop(row);

        if (top + RowHeight < Viewport.Y || top > Viewport.Bottom)
            return;

        var color = GetValue(row == SelectedIndex ? SelectionTextColorProperty : ColorProperty);
        var left = ChevronLeft(row) + 4;
        var middle = top + (RowHeight / 2);
        var expanded = visible[row].IsExpanded;

        for (var i = 0; i < 3; i++)
        {
            var length = (5 - (2 * i)) * ChevronStep;

            context.FillRect(
                expanded
                    ? new Rect(left + (i * ChevronStep), middle - (1.5 * ChevronStep) + (i * ChevronStep), length, ChevronStep)
                    : new Rect(left + (i * ChevronStep), middle - (length / 2), ChevronStep, length),
                color);
        }
    }

    /// <summary>Gets the left edge of the triangle area of a row in the TreeView's own coordinates.</summary>
    /// <param name="row">The row's index.</param>
    /// <returns>The distance from the left edge of the border box.</returns>
    double ChevronLeft(int row) => Viewport.X + TextInset + (visible[row].Depth * Indent);

    /// <summary>Follows the selection of the row that the list changed to.</summary>
    void OnRowSelected()
    {
        if (rebuilding)
            return;

        var node = SelectedIndex >= 0 && SelectedIndex < visible.Count ? visible[SelectedIndex] : null;

        if (node == selected)
            return;

        selected = node;

        SelectedNodeChanged?.Invoke(this, node);
    }

    /// <summary>Adds the visible nodes below a node in the order of the rows.</summary>
    /// <param name="node">The node, which is not added itself.</param>
    void Collect(TreeNode node)
    {
        foreach (var child in node.Children)
        {
            visible.Add(child);

            if (child.IsExpanded)
                Collect(child);
        }
    }

    /// <summary>Runs an action for every node below a node.</summary>
    /// <param name="node">The node, which the action does not run for.</param>
    /// <param name="action">The action.</param>
    static void Walk(TreeNode node, Action<TreeNode> action)
    {
        foreach (var child in node.Children)
        {
            action(child);

            Walk(child, action);
        }
    }

    /// <summary>Finds the top-level node that a node is below.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The top-level ancestor, or the node itself.</returns>
    static TreeNode Top(TreeNode node)
    {
        var top = node;

        while (top.Parent is { } parent)
            top = parent;

        return top;
    }
}
