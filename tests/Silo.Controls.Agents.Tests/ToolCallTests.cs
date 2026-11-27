using Silo;
using Xunit;

public class ToolCallTests
{
    sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    static readonly DateTimeOffset T0 = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_call_is_pending_and_has_an_id_and_no_times()
    {
        var call = new ToolCall("grep", "x");

        Assert.Equal(ToolCallStatus.Pending, call.Status);
        Assert.Equal(8, call.Id.Length);
        Assert.Null(call.StartedAt);
        Assert.Null(call.Elapsed(TimeProvider.System));
        Assert.Throws<ArgumentException>(() => new ToolCall(string.Empty));
    }

    [Fact]
    public void Start_and_Succeed_set_the_status_and_the_times_and_the_duration_follows_the_clock()
    {
        var time = new FakeTime(T0);
        var call = new ToolCall("grep");

        call.Start(time);
        time.Now = T0.AddSeconds(2);

        Assert.Equal(ToolCallStatus.Running, call.Status);
        Assert.Equal(TimeSpan.FromSeconds(2), call.Elapsed(time));

        call.Succeed(time);
        time.Now = T0.AddSeconds(10);

        Assert.Equal(ToolCallStatus.Succeeded, call.Status);
        Assert.Equal(TimeSpan.FromSeconds(2), call.Elapsed(time));
    }

    [Fact]
    public void Fail_keeps_the_message_and_Cancel_ends_the_call_without_one()
    {
        var failed = new ToolCall("a");
        var cancelled = new ToolCall("b");

        failed.Fail("boom");
        cancelled.Cancel();

        Assert.Equal(ToolCallStatus.Failed, failed.Status);
        Assert.Equal("boom", failed.Error);
        Assert.Equal(ToolCallStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.FinishedAt);
    }

    [Fact]
    public void Every_change_raises_Changed_and_a_change_to_the_same_value_does_not()
    {
        var call = new ToolCall("a", "{}");
        var changes = 0;

        call.Changed += (_, _) => changes++;

        call.Arguments = "{}";
        call.Summary = string.Empty;
        call.Output = string.Empty;

        Assert.Equal(0, changes);

        call.Arguments = "{ }";
        call.Summary = "s";
        call.AppendOutput("a");
        call.AppendOutput("b");
        call.AppendOutput(string.Empty);
        call.Status = ToolCallStatus.AwaitingApproval;
        call.AddChild(new ToolCall("child"));

        Assert.Equal(6, changes);
        Assert.Equal("ab", call.Output);
        Assert.Single(call.Children);
    }

    [Fact]
    public void The_default_summary_is_the_first_string_of_JSON_arguments_else_the_first_line()
    {
        Assert.Equal("src/a.cs", new ToolCall("a", """{ "limit": 5, "path": "src/a.cs" }""").DefaultSummary);
        Assert.Equal("first", new ToolCall("a", "first\nsecond").DefaultSummary);
        Assert.Equal("{ \"path\":", new ToolCall("a", "{ \"path\": ").DefaultSummary);
        Assert.Equal(string.Empty, new ToolCall("a").DefaultSummary);
        Assert.Equal("mine", new ToolCall("a", """{ "path": "x" }""") { Summary = "mine" }.DefaultSummary);
    }

    [Fact]
    public void A_status_an_app_makes_is_a_value_like_the_built_in_ones()
    {
        var limited = new ToolCallStatus("rate-limited", "Rate limited", ToolCallStatusKind.Waiting);

        Assert.Equal(limited, new ToolCallStatus("rate-limited", "Rate limited", ToolCallStatusKind.Waiting));
        Assert.NotEqual(ToolCallStatus.AwaitingApproval, limited);
        Assert.Equal(ToolCallStatusKind.Active, ToolCallStatus.Running.Kind);
    }
}
