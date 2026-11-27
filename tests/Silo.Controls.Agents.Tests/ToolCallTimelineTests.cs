using Silo;
using Xunit;

public class ToolCallTimelineTests
{
    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children.OfType<Control>())
        {
            yield return child;

            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }

    [Fact]
    public void An_empty_timeline_says_so_and_each_added_call_gets_a_card_in_order()
    {
        var timeline = new ToolCallTimeline();

        Assert.Equal("No tool calls", timeline.Summary);

        var first = new ToolCall("a");
        var second = new ToolCall("b");

        timeline.Add(first);
        timeline.Add(second);

        Assert.Equal([first, second], timeline.Calls);
        Assert.Equal(["a", "b"], timeline.Groups.Select(g => g.Card.Call!.Name));
        Assert.Equal("2 tool calls", timeline.Summary);
    }

    [Fact]
    public void The_summary_counts_running_and_failed_calls_including_sub_calls_and_follows_changes()
    {
        var timeline = new ToolCallTimeline();
        var parent = new ToolCall("agent");
        var child = new ToolCall("grep");

        timeline.Add(parent);

        Assert.Equal("1 tool call", timeline.Summary);

        parent.Start();
        parent.AddChild(child);
        child.Start();

        Assert.Equal("2 tool calls · 2 running", timeline.Summary);

        child.Fail("no");

        Assert.Equal("2 tool calls · 1 running · 1 failed", timeline.Summary);

        timeline.Clear();

        Assert.Equal("No tool calls", timeline.Summary);
        Assert.Empty(timeline.Groups);
    }

    [Fact]
    public void Verbosity_goes_to_every_card_that_has_not_set_its_own()
    {
        var timeline = new ToolCallTimeline();
        var parent = new ToolCall("agent");
        var child = new ToolCall("grep");

        parent.AddChild(child);
        timeline.Add(parent);
        timeline.Add(new ToolCall("other"));

        timeline.Groups[1].Card.Verbosity = ToolCallVerbosity.Minimal;
        timeline.Verbosity = ToolCallVerbosity.Detailed;

        Assert.Equal(ToolCallVerbosity.Detailed, timeline.Groups[0].Card.Verbosity);
        Assert.Equal(ToolCallVerbosity.Detailed, timeline.Groups[0].Groups[0].Card.Verbosity);
        Assert.Equal(ToolCallVerbosity.Minimal, timeline.Groups[1].Card.Verbosity);

        timeline.Groups[1].Card.ClearVerbosity();

        Assert.Equal(ToolCallVerbosity.Detailed, timeline.Groups[1].Card.Verbosity);
    }

    [Fact]
    public void Sub_calls_show_under_a_card_only_while_it_is_open_and_one_added_later_gets_its_group()
    {
        var timeline = new ToolCallTimeline();
        var parent = new ToolCall("agent");

        timeline.Add(parent);

        var group = timeline.Groups[0];

        parent.AddChild(new ToolCall("grep"));

        Assert.Single(group.Groups);
        Assert.False(group.ShowsChildren);
        Assert.Single(group.Children);

        group.Card.SetExpanded(true);

        Assert.True(group.ShowsChildren);
        Assert.Equal(2, group.Children.Count);

        parent.AddChild(new ToolCall("read"));

        Assert.Equal(2, group.Groups.Count);

        group.Card.SetExpanded(false);

        Assert.Single(group.Children);
    }

    [Fact]
    public void ExpandAll_and_CollapseAll_reach_the_cards_of_sub_calls()
    {
        var timeline = new ToolCallTimeline();
        var parent = new ToolCall("agent");

        parent.AddChild(new ToolCall("grep"));
        timeline.Add(parent);
        timeline.Add(new ToolCall("read"));

        timeline.ExpandAll();

        Assert.All(timeline.Groups, g => Assert.True(g.Card.IsExpanded));
        Assert.True(timeline.Groups[0].Groups[0].Card.IsExpanded);

        timeline.CollapseAll();

        Assert.All(timeline.Groups, g => Assert.False(g.Card.IsExpanded));
        Assert.False(timeline.Groups[0].Groups[0].Card.IsExpanded);
    }

    [Fact]
    public void Cancel_and_Retry_of_any_card_reach_the_timeline_with_the_call()
    {
        var timeline = new ToolCallTimeline();
        var parent = new ToolCall("agent");
        var child = new ToolCall("grep");
        var cancelled = new List<ToolCall>();
        var retried = new List<ToolCall>();

        parent.AddChild(child);
        timeline.Add(parent);
        timeline.CancelRequested += (_, c) => cancelled.Add(c);
        timeline.RetryRequested += (_, c) => retried.Add(c);
        timeline.ExpandAll();

        child.Start();

        var cancel = Descendants(timeline.Groups[0].Groups[0].Card.Header).OfType<Button>().Single();

        cancel.PointerDown();
        cancel.PointerUp(inside: true);

        child.Fail("no");

        var retry = Descendants(timeline.Groups[0].Groups[0].Card.Header).OfType<Button>().Single();

        retry.PointerDown();
        retry.PointerUp(inside: true);

        Assert.Equal([child], cancelled);
        Assert.Equal([child], retried);
    }

    [Fact]
    public void The_clipboard_and_the_time_source_reach_every_card_including_ones_added_later()
    {
        var time = new FakeTime();
        var timeline = new ToolCallTimeline { TimeProvider = time };
        var parent = new ToolCall("agent");

        timeline.Add(parent);
        parent.AddChild(new ToolCall("grep"));

        Assert.Same(time, timeline.Groups[0].Card.TimeProvider);
        Assert.Same(time, timeline.Groups[0].Groups[0].Card.TimeProvider);
    }

    sealed class FakeTime : TimeProvider;

    [Fact]
    public void Density_goes_to_every_card_that_has_not_set_its_own_and_to_cards_added_later()
    {
        var parent = new ToolCall("agent", "{}");
        var timeline = new ToolCallTimeline();

        timeline.Add(parent);
        timeline.Add(new ToolCall("b", "{}"));

        timeline.Groups[1].Card.Density = ToolCallDensity.Roomy;
        timeline.Density = ToolCallDensity.Compact;

        parent.AddChild(new ToolCall("child", "{}"));
        timeline.Groups[0].Card.Call = parent;

        Assert.True(timeline.HasClass("density-compact"));
        Assert.Equal(ToolCallDensity.Compact, timeline.Groups[0].Card.Density);
        Assert.Equal(ToolCallDensity.Compact, timeline.Groups[0].Groups[0].Card.Density);
        Assert.Equal(ToolCallDensity.Roomy, timeline.Groups[1].Card.Density);
        Assert.True(timeline.Groups[0].Card.HasClass("density-compact"));
        Assert.True(timeline.Groups[1].Card.HasClass("density-roomy"));

        timeline.Groups[1].Card.ClearDensity();

        Assert.True(timeline.Groups[1].Card.HasClass("density-compact"));
        Assert.False(timeline.Groups[1].Card.HasClass("density-roomy"));

        timeline.Density = ToolCallDensity.Normal;

        Assert.False(timeline.HasClass("density-compact"));
        Assert.False(timeline.Groups[0].Card.HasClass("density-compact"));
    }
}
