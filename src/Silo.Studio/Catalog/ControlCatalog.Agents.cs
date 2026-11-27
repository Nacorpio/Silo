using Silo.Layout;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>The instant that the Agents samples treat as now, so that a running call shows the same duration every time.</summary>
    static readonly DateTimeOffset SampleNow = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the entries of the Agents category.</summary>
    static IEnumerable<CatalogEntry> Agents() =>
    [
        new("ToolCallCard", "Agents", CreateToolCallCard),
        new("ToolCallTimeline", "Agents", CreateToolCallTimeline),
    ];

    /// <summary>A time source that always says the same time.</summary>
    /// <param name="now">The time.</param>
    sealed class FrozenTime(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Creates a card for a call that succeeded.</summary>
    /// <returns>The card, closed.</returns>
    static ToolCallCard CreateToolCallCard()
    {
        var call = new ToolCall("read_file", """{ "path": "src/Silo.Controls/Button.cs" }""");
        var time = new FrozenTime(SampleNow);

        call.StartedAt = SampleNow - TimeSpan.FromMilliseconds(340);
        call.Output = "namespace Silo;\n\npublic class Button : TextControl\n{\n}\n";
        call.Succeed(time);

        return new ToolCallCard { Call = call, TimeProvider = time };
    }

    /// <summary>Creates a timeline with a call in each status and a sub-agent with calls of its own.</summary>
    /// <returns>The timeline.</returns>
    static ToolCallTimeline CreateToolCallTimeline()
    {
        var time = new FrozenTime(SampleNow);
        var timeline = new ToolCallTimeline { TimeProvider = time };

        var read = new ToolCall("read_file", """{ "path": "src/Program.cs" }""") { Output = "using Silo;\n" };

        read.StartedAt = SampleNow - TimeSpan.FromSeconds(3);
        read.Succeed(time);

        var tests = new ToolCall("run_tests", """{ "filter": "Silo.Controls" }""") { StartedAt = SampleNow - TimeSpan.FromSeconds(2.4) };

        tests.Status = ToolCallStatus.Running;
        tests.Output = "Running 1,139 tests...\n";

        var build = new ToolCall("build", """{ "project": "Silo.Studio" }""") { Error = "error CS0103: The name 'x' does not exist" };

        build.StartedAt = SampleNow - TimeSpan.FromSeconds(9);
        build.FinishedAt = SampleNow - TimeSpan.FromSeconds(4);
        build.Status = ToolCallStatus.Failed;

        var push = new ToolCall("git_push", """{ "branch": "main" }""") { Status = ToolCallStatus.AwaitingApproval };

        var agent = new ToolCall("sub_agent", """{ "task": "Find where the cascade is applied" }""");
        var grep = new ToolCall("grep", """{ "pattern": "ApplyStyles" }""") { Output = "src/Silo.Core/Element.cs\n" };

        grep.StartedAt = SampleNow - TimeSpan.FromSeconds(1);
        grep.Succeed(time);
        agent.StartedAt = SampleNow - TimeSpan.FromSeconds(5);
        agent.Status = ToolCallStatus.Running;
        agent.AddChild(grep);

        foreach (var call in new[] { read, tests, build, push, agent })
            timeline.Add(call);

        timeline.Groups[2].Card.SetExpanded(true);
        timeline.Groups[4].Card.SetExpanded(true);

        return timeline;
    }
}
