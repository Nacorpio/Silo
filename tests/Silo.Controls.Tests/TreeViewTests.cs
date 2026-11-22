using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class TreeViewTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "align-items": "start" },
            "TreeView": { "width": "200px", "height": "100px", "item-height": "20px", "indent": "10px" }
          }
        }
        """;

    static (Box Root, TreeView Tree, TreeNode Src, TreeNode Docs) Setup()
    {
        var tree = new TreeView();
        var src = tree.Add(new TreeNode("src"));
        var docs = tree.Add(new TreeNode("docs"));

        src.Add(new TreeNode("Program.cs"));
        src.Add(new TreeNode("Views")).Add(new TreeNode("Main.cs"));
        docs.Add(new TreeNode("index.md"));

        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(tree);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        return (root, tree, src, docs);
    }

    [Fact]
    public void Only_the_top_level_nodes_are_shown_while_everything_is_collapsed()
    {
        var (_, tree, _, _) = Setup();

        Assert.Equal(["src", "docs"], tree.Items);
    }

    [Fact]
    public void Expanding_a_node_shows_its_children_below_it_and_raises_the_event()
    {
        var (_, tree, src, _) = Setup();
        var expanded = new List<TreeNode>();

        tree.NodeExpandedChanged += (_, node) => expanded.Add(node);

        src.IsExpanded = true;

        Assert.Equal(["src", "Program.cs", "Views", "docs"], tree.Items);
        Assert.Equal([src], expanded);

        src.IsExpanded = false;

        Assert.Equal(["src", "docs"], tree.Items);
    }

    [Fact]
    public void Nodes_know_their_parent_and_depth()
    {
        var (_, _, src, _) = Setup();
        var views = src.Children[1];

        Assert.Null(src.Parent);
        Assert.Same(src, views.Parent);
        Assert.Equal(0, src.Depth);
        Assert.Equal(2, views.Children[0].Depth);
        Assert.True(src.Contains(views.Children[0]));
        Assert.False(views.Contains(src));
    }

    [Fact]
    public void Selecting_a_node_selects_its_row_and_raises_the_event()
    {
        var (_, tree, src, _) = Setup();
        var changes = new List<TreeNode?>();

        tree.SelectedNodeChanged += (_, node) => changes.Add(node);

        tree.SelectedNode = src.Children[0];

        Assert.Same(src.Children[0], tree.SelectedNode);
        Assert.Equal(1, tree.SelectedIndex);
        Assert.True(src.IsExpanded);
        Assert.Equal([src.Children[0]], changes);
    }

    [Fact]
    public void The_selection_follows_its_node_when_rows_above_it_appear()
    {
        var (_, tree, src, docs) = Setup();

        tree.SelectedNode = docs;
        src.IsExpanded = true;

        Assert.Same(docs, tree.SelectedNode);
        Assert.Equal(3, tree.SelectedIndex);
    }

    [Fact]
    public void Collapsing_an_ancestor_of_the_selected_node_selects_the_ancestor()
    {
        var (_, tree, src, _) = Setup();
        var changes = new List<TreeNode?>();

        tree.SelectedNode = src.Children[0];
        tree.SelectedNodeChanged += (_, node) => changes.Add(node);

        src.IsExpanded = false;

        Assert.Same(src, tree.SelectedNode);
        Assert.Equal([src], changes);
    }

    [Fact]
    public void Right_expands_then_moves_to_the_first_child_and_Left_collapses_then_moves_to_the_parent()
    {
        var (_, tree, src, _) = Setup();

        tree.SelectedNode = src;
        tree.KeyDown(Key.Right);

        Assert.True(src.IsExpanded);

        tree.KeyDown(Key.Right);

        Assert.Same(src.Children[0], tree.SelectedNode);

        tree.KeyDown(Key.Left);

        Assert.Same(src, tree.SelectedNode);

        tree.KeyDown(Key.Left);

        Assert.False(src.IsExpanded);
    }

    [Fact]
    public void Enter_toggles_a_node_with_children_and_activates_a_leaf()
    {
        var (_, tree, src, _) = Setup();
        var activated = new List<TreeNode>();

        tree.NodeActivated += (_, node) => activated.Add(node);
        tree.SelectedNode = src;

        tree.KeyDown(Key.Enter);

        Assert.True(src.IsExpanded);

        tree.SelectedNode = src.Children[0];
        tree.KeyDown(Key.Enter);

        Assert.Equal([src.Children[0]], activated);
    }

    [Fact]
    public void The_arrow_keys_move_through_the_visible_rows()
    {
        var (_, tree, src, docs) = Setup();

        src.IsExpanded = true;
        tree.SelectedNode = src;

        tree.KeyDown(Key.Down);
        tree.KeyDown(Key.Down);
        tree.KeyDown(Key.Down);

        Assert.Same(docs, tree.SelectedNode);
    }

    [Fact]
    public void Pressing_the_triangle_toggles_the_node_and_pressing_the_text_only_selects_it()
    {
        var (root, tree, src, _) = Setup();
        var router = new PointerRouter(root);

        router.Down(10, 10);
        router.Up(10, 10);

        Assert.True(src.IsExpanded);
        Assert.Same(src, tree.SelectedNode);

        router.Down(60, 10);
        router.Up(60, 10);

        Assert.True(src.IsExpanded);
    }

    [Fact]
    public void ExpandAll_and_CollapseAll_change_every_node()
    {
        var (_, tree, src, _) = Setup();

        tree.ExpandAll();

        Assert.Equal(["src", "Program.cs", "Views", "Main.cs", "docs", "index.md"], tree.Items);

        tree.SelectedNode = src.Children[1].Children[0];
        tree.CollapseAll();

        Assert.Equal(["src", "docs"], tree.Items);
        Assert.Same(src, tree.SelectedNode);
    }

    [Fact]
    public void Removing_a_node_removes_its_rows_and_clears_the_selection_when_it_was_inside()
    {
        var (_, tree, src, _) = Setup();
        var changes = new List<TreeNode?>();

        tree.SelectedNode = src.Children[0];
        tree.SelectedNodeChanged += (_, node) => changes.Add(node);

        Assert.True(tree.Remove(src));

        Assert.Equal(["docs"], tree.Items);
        Assert.Null(tree.SelectedNode);
        Assert.Equal([null], changes);
    }

    [Fact]
    public void A_node_cannot_be_added_twice_and_a_foreign_node_cannot_be_selected()
    {
        var (_, tree, src, _) = Setup();

        Assert.Throws<InvalidOperationException>(() => tree.Add(src));
        Assert.Throws<ArgumentException>(() => tree.SelectedNode = new TreeNode("loose"));
    }

    [Fact]
    public void Changing_the_text_of_a_node_updates_its_row()
    {
        var (_, tree, src, _) = Setup();

        src.Text = "source";

        Assert.Equal(["source", "docs"], tree.Items);
    }

    [Fact]
    public void Each_level_moves_the_row_right_by_the_indent()
    {
        var (root, tree, src, _) = Setup();
        var builder = new DisplayListBuilder();

        src.IsExpanded = true;
        tree.ExpandAll();
        root.Arrange(new Size(300, 200));
        root.Render(builder);

        var xs = builder.Build().Commands.OfType<DrawTextCommand>().ToDictionary(c => c.Text, c => c.X);

        Assert.Equal(10, xs["Program.cs"] - xs["src"]);
        Assert.Equal(10, xs["Main.cs"] - xs["Program.cs"]);
    }
}
