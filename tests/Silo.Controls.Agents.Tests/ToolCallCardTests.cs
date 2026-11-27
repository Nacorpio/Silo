using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Validation;
using Xunit;

public class ToolCallCardTests
{
    sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class FakeClipboard : IClipboard
    {
        public string Text { get; set; } = string.Empty;

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    static readonly DateTimeOffset T0 = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children.OfType<Control>())
        {
            yield return child;

            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }

    static bool Has(Control root, Control part) => Descendants(root).Contains(part);

    static ToolCallCard Card(ToolCall call) => new() { Call = call };

    static IEnumerable<string> Texts(Control root) => Descendants(root).OfType<TextBlock>().Select(t => t.Text);

    [Fact]
    public void A_card_starts_as_one_row_and_pressing_it_opens_and_closes_the_body()
    {
        var card = Card(new ToolCall("read_file", """{ "path": "a.cs" }""") { Output = "text" });
        var changes = 0;

        card.ExpandedChanged += (_, _) => changes++;

        Assert.False(card.IsExpanded);
        Assert.Single(card.Children);

        card.Header.PointerDown(new PointerEvent(5, 5));

        Assert.True(card.IsExpanded);
        Assert.Contains("expanded", card.PseudoStates);
        Assert.Equal(2, card.Children.Count);

        card.Header.PointerDown(new PointerEvent(5, 5));

        Assert.False(card.IsExpanded);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Enter_and_the_space_bar_on_the_row_open_the_card()
    {
        var card = Card(new ToolCall("a", "x") { Output = "o" });

        Assert.True(card.Header.KeyDown(Key.Enter, KeyModifiers.None));
        Assert.True(card.IsExpanded);
        Assert.True(card.Header.TextInput(" "));
        Assert.False(card.IsExpanded);
        Assert.False(card.Header.TextInput("a"));
        Assert.False(card.Header.KeyDown(Key.Left, KeyModifiers.None));
    }

    [Fact]
    public void The_body_shows_pretty_printed_arguments_and_the_output_and_follows_streamed_output()
    {
        var call = new ToolCall("run", """{"a":1}""");
        var card = Card(call);

        card.SetExpanded(true);
        call.AppendOutput("line 1\n");
        call.AppendOutput("line 2\n");

        var areas = Descendants(card).OfType<TextArea>().ToList();

        Assert.Equal(2, areas.Count);
        Assert.All(areas, a => Assert.True(a.IsReadOnly));
        Assert.Equal("{\n  \"a\": 1\n}", areas[0].Buffer.Text.Replace("\r\n", "\n"));
        Assert.Equal("line 1\nline 2\n", areas[1].Buffer.Text);
    }

    [Fact]
    public void A_call_that_fails_opens_its_card_once_and_not_after_a_person_closed_it()
    {
        var call = new ToolCall("build");
        var card = Card(call);

        call.Start();
        call.Fail("boom");

        Assert.True(card.IsExpanded);
        Assert.Contains("failed", card.PseudoStates);

        card.Header.PointerDown(new PointerEvent(1, 1));
        call.Error = "still boom";

        Assert.False(card.IsExpanded);

        var other = new ToolCall("x");
        var second = Card(other);

        second.Header.PointerDown(new PointerEvent(1, 1));
        second.Header.PointerDown(new PointerEvent(1, 1));
        other.Fail("late");

        Assert.False(second.IsExpanded);
    }

    [Fact]
    public void A_card_made_for_a_call_that_already_failed_is_open()
    {
        var call = new ToolCall("x");

        call.Fail("boom");

        Assert.True(Card(call).IsExpanded);
    }

    [Fact]
    public void The_verbosities_decide_what_is_shown()
    {
        var call = new ToolCall("x", "arg") { Output = "o" };
        var card = Card(call);

        card.Verbosity = ToolCallVerbosity.Minimal;
        card.Header.PointerDown(new PointerEvent(1, 1));

        Assert.False(card.IsExpanded);
        Assert.DoesNotContain(Descendants(card.Header), c => c is ToolCallSummary);

        card.Verbosity = ToolCallVerbosity.Detailed;

        Assert.True(card.IsExpanded);
        Assert.Contains(Descendants(card), c => c is TextArea);
        Assert.DoesNotContain(Texts(card), t => t.Contains('·'));

        card.Verbosity = ToolCallVerbosity.Debug;

        Assert.Contains(Texts(card), t => t.Contains(call.Id) && t.Contains("pending"));
        Assert.False(card.Header.KeyDown(Key.Enter, KeyModifiers.None) && !card.IsExpanded);
    }

    [Fact]
    public void A_status_gives_the_card_its_class_and_its_state_and_a_status_an_app_made_works_the_same()
    {
        var call = new ToolCall("x");
        var card = Card(call);

        call.Status = ToolCallStatus.Running;

        Assert.Contains("status-running", card.Classes);
        Assert.Contains("running", card.PseudoStates);

        call.Status = new ToolCallStatus("rate-limited", "Rate limited", ToolCallStatusKind.Waiting);

        Assert.DoesNotContain("status-running", card.Classes);
        Assert.Contains("status-rate-limited", card.Classes);
        Assert.Contains("waiting", card.PseudoStates);
        Assert.DoesNotContain("running", card.PseudoStates);

        call.Status = ToolCallStatus.Succeeded;

        Assert.Contains("succeeded", card.PseudoStates);
        Assert.DoesNotContain("waiting", card.PseudoStates);
    }

    [Fact]
    public void Only_a_running_call_animates_and_its_duration_follows_the_time_source()
    {
        var time = new FakeTime(T0);
        var call = new ToolCall("x");
        var card = new ToolCallCard { Call = call, TimeProvider = time };

        Assert.False(card.IsAnimating);

        call.Start(time);

        Assert.True(card.IsAnimating);

        time.Now = T0.AddMilliseconds(1500);

        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(card);
        root.Arrange(new Size(400, 200));
        root.Render(new DisplayListBuilder());

        Assert.Contains("1.5 s", Texts(card));

        call.Succeed(time);

        Assert.False(card.IsAnimating);
    }

    [Fact]
    public void Durations_are_written_for_people()
    {
        Assert.Equal("0 ms", ToolCallCard.FormatDuration(TimeSpan.Zero));
        Assert.Equal("340 ms", ToolCallCard.FormatDuration(TimeSpan.FromMilliseconds(340)));
        Assert.Equal("1.2 s", ToolCallCard.FormatDuration(TimeSpan.FromSeconds(1.23)));
        Assert.Equal("5 s", ToolCallCard.FormatDuration(TimeSpan.FromSeconds(5)));
        Assert.Equal("2m 05s", ToolCallCard.FormatDuration(TimeSpan.FromSeconds(125)));
    }

    [Fact]
    public void Cancel_shows_while_the_call_runs_and_Retry_after_it_failed_and_each_raises_its_event_with_the_call()
    {
        var call = new ToolCall("x");
        var card = Card(call);
        var cancelled = new List<ToolCall>();
        var retried = new List<ToolCall>();

        card.CancelRequested += (_, c) => cancelled.Add(c);
        card.RetryRequested += (_, c) => retried.Add(c);

        Assert.Empty(Descendants(card.Header).OfType<Button>());

        call.Start();

        var cancel = Descendants(card.Header).OfType<Button>().Single();

        cancel.PointerDown();
        cancel.PointerUp(inside: true);

        call.Fail("no");

        var retry = Descendants(card.Header).OfType<Button>().Single();

        retry.PointerDown();
        retry.PointerUp(inside: true);

        Assert.Equal([call], cancelled);
        Assert.Equal([call], retried);
        Assert.Equal(ToolCallStatus.Failed, call.Status);
    }

    [Fact]
    public void Copy_output_writes_the_output_to_the_clipboard()
    {
        var clipboard = new FakeClipboard();
        var card = new ToolCallCard { Call = new ToolCall("x") { Output = "result" }, Clipboard = clipboard };

        card.SetExpanded(true);

        var copy = Descendants(card).OfType<Button>().Single(b => b.Classes.Contains("copy"));

        copy.PointerDown();
        copy.PointerUp(inside: true);

        Assert.Equal("result", clipboard.Text);
    }

    [Fact]
    public void A_waiting_call_says_what_it_waits_for_in_the_body()
    {
        var card = Card(new ToolCall("git_push") { Status = ToolCallStatus.AwaitingApproval });

        card.SetExpanded(true);

        Assert.Contains("Waiting for approval", Texts(card));
    }

    [Fact]
    public void A_card_with_a_theme_draws_its_name_summary_and_duration()
    {
        var time = new FakeTime(T0);
        var call = new ToolCall("read_file", """{ "path": "src/a.cs" }""");
        var card = new ToolCallCard { Call = call, TimeProvider = time };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        call.StartedAt = T0.AddMilliseconds(-340);
        call.Succeed(time);
        root.Children.Add(card);
        card.AddClass("wide");
        root.ApplyStyles(new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), AgentsTheme.Load(), JsoncStylesheetReader.Read("""{ "rules": { "ToolCallCard.wide": { "width": "400px" } } }""", "wide.jsonc")]));
        root.Arrange(new Size(400, 200));

        var builder = new DisplayListBuilder();

        root.Render(builder);

        var texts = builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text).ToList();

        Assert.Contains("read_file", texts);
        Assert.Contains("src/a.cs", texts);
        Assert.Contains("340 ms", texts);
    }

    [Fact]
    public void The_agents_theme_is_valid_against_the_default_theme()
    {
        StylesheetValidator.AddControls(typeof(ToolCallCard).Assembly);

        var issues = StylesheetValidator.ValidateText(AgentsTheme.Source(), AgentsTheme.FileName, [DefaultTheme.Load()]);

        Assert.Empty(issues);
    }
}
