using System.Text.Json;
using Silo;
using Silo.Diagnostics;
using Silo.Inspector;
using Silo.Styling;
using Xunit;

public class InspectorTests
{
    static InspectorNode Sample() => new(1, "Box", "root", [], [new InspectorNode(2, "Button", "ok", ["primary"], [])]);

    [Fact]
    public void A_node_is_parsed_from_a_snapshot_and_labelled_with_its_name_and_classes()
    {
        var json = JsonDocument.Parse("""{"id":1,"type":"Box","name":"root","classes":[],"pseudoStates":[],"children":[{"id":2,"type":"Button","classes":["primary","wide"],"pseudoStates":[],"children":[]}]}""").RootElement;

        var node = InspectorNode.Parse(json);

        Assert.Equal("Box#root", node.Label);
        Assert.Equal("Button.primary.wide", node.Children[0].Label);
    }

    [Fact]
    public void Property_labels_say_where_the_value_came_from()
    {
        var json = JsonDocument.Parse("""{"properties":[{"name":"gap","value":"3px","origin":"local"},{"name":"background","value":"#3b82f6","origin":"stylesheet","declared":"var(--accent)","source":{"file":"app.jsonc","line":5,"column":3}},{"name":"opacity","value":"1","origin":"default"}]}""").RootElement;

        var labels = InspectorProperty.ParseAll(json).Select(p => p.Label).ToList();

        Assert.Equal("gap: 3px   (local value)", labels[0]);
        Assert.Equal("background: #3b82f6   (var(--accent) at app.jsonc:5:3)", labels[1]);
        Assert.Equal("opacity: 1", labels[2]);
    }

    [Fact]
    public void Setting_the_tree_adds_a_row_per_node_and_clicking_a_row_selects_it()
    {
        var view = new InspectorView();

        int? selected = null;

        view.NodeSelected += id => selected = id;
        view.SetTree(Sample());

        Assert.Equal(2, view.RowCount);

        var tree = (Box)((ScrollViewer)view.Children[0]).Content!;
        var row = (Button)tree.Children[1];

        row.PointerDown();
        row.PointerUp(inside: true);

        Assert.Equal(2, selected);
    }

    [Fact]
    public void Showing_properties_highlights_the_selected_row_and_fills_the_panel()
    {
        var view = new InspectorView();

        view.SetTree(Sample());
        view.SetProperties(2, "Button#ok", [new InspectorProperty("gap", "3px", "local", null, null)]);

        var tree = (Box)((ScrollViewer)view.Children[0]).Content!;
        var details = (Box)((ScrollViewer)view.Children[1]).Content!;

        Assert.False(tree.Children[0].HasClass("selected"));
        Assert.True(tree.Children[1].HasClass("selected"));
        Assert.Equal(2, details.Children.Count);
        Assert.Equal("Button#ok", ((TextBlock)details.Children[0]).Text);
    }

    [Fact]
    public void Changes_raise_the_Changed_event_so_the_host_can_redraw()
    {
        var view = new InspectorView();

        var changes = 0;

        view.Changed += (_, _) => changes++;
        view.SetTree(Sample());

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task The_controller_fills_the_view_from_a_live_agent_over_the_pipe()
    {
        var app = new Box { Name = "app" };

        app.Children.Add(new Button { Name = "save", Text = "Save" });

        var agent = new DiagnosticsAgent(app, () => new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "Button": { "padding": "4px" } } }""", "app.jsonc")]));

        using var server = new DiagnosticsServer(agent, work => Task.FromResult(work()), $"silo-test-{Guid.NewGuid():N}");

        server.Start();
        app.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "Button": { "padding": "4px" } } }""", "app.jsonc")]));

        await using var client = await DiagnosticsClient.ConnectAsync(server.PipeName);

        var view = new InspectorView();
        var controller = new InspectorController(client, view, work => work());

        await controller.RefreshAsync();

        Assert.Equal(2, view.RowCount);

        await controller.SelectAsync(2);

        var details = (Box)((ScrollViewer)view.Children[1]).Content!;
        var lines = details.Children.Cast<TextBlock>().Select(t => t.Text).ToList();

        Assert.Equal("Button#save", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("padding:") && l.Contains("app.jsonc:1:"));
    }
}
