using System.Text.Json;
using Silo;
using Silo.Diagnostics;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class DiagnosticsAgentTests
{
    sealed class FixedMeasurer : ITextMeasurer
    {
        public Silo.Layout.Size Measure(string text, TextStyle style) => new(text.Length * 7, 14);
    }

    const string Sheet = """
        {
          "tokens": { "--accent": "#3b82f6" },
          "rules": {
            "Box": { "align-items": "start" },
            "Button": { "background": "var(--accent)", "padding": "5px" }
          }
        }
        """;

    static (DiagnosticsAgent Agent, Box Root, Button Button) Setup()
    {
        var button = new Button { Name = "ok", Text = "OK" };
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "app.jsonc")]);

        button.AddClass("primary");
        root.Children.Add(button);
        root.ApplyStyles(resolver);
        root.Arrange(new Silo.Layout.Size(200, 100));

        return (new DiagnosticsAgent(root, () => resolver), root, button);
    }

    static JsonElement Call(DiagnosticsAgent agent, string method, string? parameters = null)
    {
        var request = $$"""{"jsonrpc":"2.0","id":7,"method":"{{method}}"{{(parameters is null ? "" : $",\"params\":{parameters}")}}}""";

        return JsonDocument.Parse(agent.Handle(request)!).RootElement;
    }

    [Fact]
    public void Hello_reports_the_protocol_version()
    {
        var (agent, _, _) = Setup();

        var reply = Call(agent, "hello");

        Assert.Equal("2.0", reply.GetProperty("jsonrpc").GetString());
        Assert.Equal(7, reply.GetProperty("id").GetInt32());
        Assert.Equal(DiagnosticsAgent.ProtocolVersion, reply.GetProperty("result").GetProperty("protocol").GetInt32());
    }

    [Fact]
    public void A_snapshot_describes_the_tree_with_names_classes_and_bounds()
    {
        var (agent, _, _) = Setup();

        var root = Call(agent, "tree.snapshot").GetProperty("result");
        var button = root.GetProperty("children")[0];

        Assert.Equal("Box", root.GetProperty("type").GetString());
        Assert.Equal("root", root.GetProperty("name").GetString());
        Assert.Equal("Button", button.GetProperty("type").GetString());
        Assert.Equal("primary", button.GetProperty("classes")[0].GetString());
        Assert.Equal(24, button.GetProperty("bounds").GetProperty("width").GetDouble());
        Assert.NotEqual(root.GetProperty("id").GetInt32(), button.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Ids_stay_the_same_between_snapshots_and_a_snapshot_can_start_at_an_id()
    {
        var (agent, _, _) = Setup();

        var first = Call(agent, "tree.snapshot").GetProperty("result").GetProperty("children")[0];
        var id = first.GetProperty("id").GetInt32();

        var second = Call(agent, "tree.snapshot", $$"""{"id":{{id}}}""").GetProperty("result");

        Assert.Equal(id, second.GetProperty("id").GetInt32());
        Assert.Equal("Button", second.GetProperty("type").GetString());
    }

    [Fact]
    public void The_cascade_trace_names_the_winning_stylesheet_line()
    {
        var (agent, _, _) = Setup();

        var snapshot = Call(agent, "tree.snapshot").GetProperty("result").GetProperty("children")[0];
        var id = snapshot.GetProperty("id").GetInt32();

        var properties = Call(agent, "element.cascade", $$"""{"id":{{id}}}""").GetProperty("result").GetProperty("properties");
        var background = properties.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "background");

        Assert.Equal("stylesheet", background.GetProperty("origin").GetString());
        Assert.Equal("var(--accent)", background.GetProperty("declared").GetString());
        Assert.Equal("#3b82f6", background.GetProperty("computed").GetString());
        Assert.Equal("app.jsonc", background.GetProperty("source").GetProperty("file").GetString());
        Assert.Equal(5, background.GetProperty("source").GetProperty("line").GetInt32());
    }

    [Fact]
    public void Local_values_and_defaults_are_told_apart_from_stylesheet_values()
    {
        var (agent, _, button) = Setup();

        button.SetValue(Control.GapProperty, new Silo.Styling.Values.Length(3, Silo.Styling.Values.LengthUnit.Px));

        var id =
            Call(agent, "tree.snapshot").GetProperty("result").GetProperty("children")[0].GetProperty("id").GetInt32();
        var properties = Call(agent, "element.cascade", $$"""{"id":{{id}}}""").GetProperty("result").GetProperty("properties");

        string Origin(string name) => properties.EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == name)
            .GetProperty("origin")
            .GetString()!;

        Assert.Equal("local", Origin("gap"));
        Assert.Equal("default", Origin("opacity"));
    }

    [Fact]
    public void Bad_requests_get_JSON_RPC_errors()
    {
        var (agent, _, _) = Setup();

        var malformed = JsonDocument.Parse(agent.Handle("{ nope")!).RootElement;
        var invalid = JsonDocument.Parse(agent.Handle("""{"jsonrpc":"2.0","id":1}""")!).RootElement;

        Assert.Equal(-32700, malformed.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(-32600, invalid.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(-32601, Call(agent, "nope").GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(-32602, Call(agent, "element.cascade", """{"id":999}""").GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public void A_request_without_an_id_is_a_notification_and_gets_no_reply()
    {
        var (agent, _, _) = Setup();

        Assert.Null(agent.Handle("""{"jsonrpc":"2.0","method":"hello"}"""));
    }
}
