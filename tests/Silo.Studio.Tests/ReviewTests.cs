using Silo;
using Silo.Studio;
using Silo.Styling;
using Xunit;

public class ReviewTests
{
    const string Sample = """
        {
          // a comment
          "control": "ToolCallCard",
          "pr": "PR 1",
          "questions": [
            { "id": "a", "kind": "rating", "prompt": "Rate it", },
            { "id": "b", "kind": "yesno", "prompt": "Ok?", "hint": "Try", "variant": "Failed", "state": "hover" },
            { "id": "c", "kind": "choice", "prompt": "Pick", "options": ["x", "y"] },
            { "id": "d", "kind": "text", "prompt": "Say" }
          ]
        }
        """;

    static T Find<T>(Control root, string name) where T : Control
    {
        if (root.Name == name && root is T match)
            return match;

        foreach (var child in root.Children.OfType<Control>())
            if (FindOrNull<T>(child, name) is { } found)
                return found;

        throw new InvalidOperationException(name);
    }

    static T? FindOrNull<T>(Control root, string name) where T : Control
    {
        if (root.Name == name && root is T match)
            return match;

        return root.Children.OfType<Control>().Select(c => FindOrNull<T>(c, name)).FirstOrDefault(c => c is not null);
    }

    [Fact]
    public void A_document_reads_comments_trailing_commas_and_every_question_kind()
    {
        var document = ReviewDocument.Parse(Sample);

        Assert.Equal("ToolCallCard", document.Control);
        Assert.Equal("Review of ToolCallCard", document.Title);
        Assert.Equal("PR 1", document.Pr);
        Assert.Equal(
            [ReviewQuestionKind.Rating, ReviewQuestionKind.YesNo, ReviewQuestionKind.Choice, ReviewQuestionKind.Text],
            document.Questions.Select(q => q.Kind));
        Assert.Equal("Failed", document.Questions[1].Variant);
        Assert.Equal("hover", document.Questions[1].State);
        Assert.Equal(["x", "y"], document.Questions[2].Options);
    }

    [Theory]
    [InlineData("""{ "questions": [] }""")]
    [InlineData("""{ "control": "X", "questions": [ { "id": "a", "kind": "dial", "prompt": "p" } ] }""")]
    [InlineData("""{ "control": "X", "questions": [ { "id": "a", "kind": "choice", "prompt": "p", "options": ["one"] } ] }""")]
    [InlineData("""{ "control": "X", "questions": [ { "id": "a", "kind": "text", "prompt": "p" }, { "id": "a", "kind": "text", "prompt": "p" } ] }""")]
    [InlineData("not json")]
    public void A_document_that_is_wrong_is_a_FormatException(string text)
        => Assert.Throws<FormatException>(() => ReviewDocument.Parse(text));

    [Fact]
    public void The_questions_that_ship_with_Studio_read_and_name_variants_that_exist()
    {
        foreach (var control in new[] { "ToolCallCard", "ToolCallTimeline" })
        {
            var document = ReviewMode.Load(control, null);
            var variants = ControlCatalog.VariantsOf(control).Select(v => v.Name).ToList();

            Assert.Equal(control, document.Control);
            Assert.All(document.Questions.Where(q => q.Variant is not null), q => Assert.Contains(q.Variant, variants));
        }
    }

    [Fact]
    public void A_Control_without_its_own_questions_gets_the_general_ones()
    {
        var document = ReviewMode.Load("Button", null);

        Assert.Equal("Button", document.Control);
        Assert.NotEmpty(document.Questions);
    }

    [Fact]
    public void Answers_are_stored_replaced_and_removed_and_leave_a_JSON_file()
    {
        var answers = new ReviewAnswers("ToolCallCard");
        var at = new DateTimeOffset(2026, 11, 27, 18, 0, 0, TimeSpan.Zero);

        answers.Set("a", "4", null);
        answers.Set("b", "Yes", "  ");
        answers.Set("b", "No", "slow");
        answers.Set("c", string.Empty, null);
        answers.Comment = "hi";
        answers.MarkViewed("Failed", "hover");
        answers.MarkViewed("Failed", "hover");

        Assert.Equal(2, answers.Count);
        Assert.Equal("No", answers.Get("b"));
        Assert.Equal(["Failed/hover"], answers.Viewed);

        using var json = System.Text.Json.JsonDocument.Parse(answers.ToJson(at));

        Assert.Equal("hi", json.RootElement.GetProperty("comment").GetString());
        Assert.Equal("slow", json.RootElement.GetProperty("answers").GetProperty("b").GetProperty("note").GetString());
        Assert.False(json.RootElement.GetProperty("answers").GetProperty("a").TryGetProperty("note", out _));

        var folder = Path.Combine(Path.GetTempPath(), "silo-review-test-" + Guid.NewGuid().ToString("N"));
        var path = answers.Save(folder, at);

        Assert.Equal("review-ToolCallCard-20261127-180000.json", Path.GetFileName(path));
        Assert.True(File.Exists(path));

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void A_second_lock_for_the_same_Control_closes_the_first_and_a_stale_or_own_file_closes_nothing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "silo-review-test-" + Guid.NewGuid().ToString("N"));
        var closed = new List<int>();

        using (var first = ReviewLock.Acquire("A", folder, id: 100, close: closed.Add))
        {
            Assert.Null(first.Closed);

            using var second = ReviewLock.Acquire("A", folder, id: 200, close: closed.Add);

            Assert.Equal(100, second.Closed);
            Assert.Equal([100], closed);

            using var other = ReviewLock.Acquire("B", folder, id: 300, close: closed.Add);

            Assert.Null(other.Closed);
        }

        Assert.False(File.Exists(Path.Combine(folder, "A.pid")));

        File.WriteAllText(Path.Combine(folder, "A.pid"), "200");

        using (var same = ReviewLock.Acquire("A", folder, id: 200, close: closed.Add))
            Assert.Null(same.Closed);

        Assert.Equal([100], closed);

        File.WriteAllText(Path.Combine(folder, "A.pid"), "777");

        using (var stale = ReviewLock.Acquire("A", folder, id: 200, close: closed.Add))
            Assert.Equal(777, stale.Closed);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void The_view_shows_the_first_sample_and_the_pickers_switch_variant_and_state()
    {
        var view = new ReviewView(ReviewMode.Load("ToolCallCard", null));

        Assert.IsType<ToolCallCard>(view.Sample);
        Assert.Equal("Succeeded", view.CurrentVariant);
        Assert.Equal("normal", view.CurrentState);

        var first = view.Sample;

        view.Show("Failed", "normal");

        Assert.NotSame(first, view.Sample);
        Assert.Equal("Failed", view.CurrentVariant);
        Assert.Equal("normal", view.CurrentState);
        Assert.Contains("Failed/normal", view.Answers.Viewed);

        view.Show("Nope", "nope");

        Assert.Equal("Failed", view.CurrentVariant);
    }

    [Fact]
    public void Moving_to_a_question_switches_the_sample_and_the_panel_records_answers()
    {
        var view = new ReviewView(ReviewMode.Load("ToolCallCard", null));
        var statusStep = view.Panel.Document.Questions.ToList().FindIndex(q => q.Id == "status");

        view.Panel.GoTo(statusStep);

        Assert.Equal("Running", view.CurrentVariant);

        view.Panel.GoTo(0);

        var rating = Find<Box>(view.Panel, "question-look").Children.OfType<Rating>().Single();

        rating.Value = 4;

        Assert.Equal("4", view.Answers.Get("look"));

        rating.Value = 0;

        Assert.Null(view.Answers.Get("look"));

        view.Panel.GoTo(view.Panel.SummaryStep);

        var comment = Find<TextArea>(view.Panel, "comment-text");

        comment.TextInput("note");

        Assert.Equal("note", view.Answers.Comment);
    }

    [Fact]
    public void The_wizard_shows_one_question_at_a_time_and_skipped_and_answered_steps_are_marked()
    {
        var view = new ReviewView(ReviewMode.Load("ToolCallCard", null));
        var panel = view.Panel;
        var count = panel.Document.Questions.Count;

        Assert.Equal(0, panel.Step);
        Assert.DoesNotContain(panel.BackButton, panel.Children.SelectMany(c => c.Children));

        panel.Next();

        Assert.Equal(1, panel.Step);

        var dots = Find<Box>(panel, "review-steps").Children.OfType<Button>().ToList();

        Assert.Equal(count + 1, dots.Count);
        Assert.True(dots[1].HasClass("current"));
        Assert.True(dots[0].HasClass("skipped"));

        panel.GoTo(0);
        Find<Box>(panel, "question-look").Children.OfType<Rating>().Single().Value = 3;
        panel.GoTo(1);

        Assert.True(Find<Box>(panel, "review-steps").Children.OfType<Button>().First().HasClass("answered"));

        panel.GoTo(99);

        Assert.Equal(1, panel.Step);

        panel.GoTo(panel.SummaryStep);

        Assert.True(panel.IsSummary);
        Assert.Null(panel.Current);
        Assert.Contains(panel.SubmitButton, Find<Box>(panel, "review-summary").Children);
    }

    [Fact]
    public void Keys_answer_the_question_and_Enter_goes_on_only_once_it_has_an_answer()
    {
        var view = new ReviewView(ReviewMode.Load("ChatInput", null));
        var panel = view.Panel;
        var yesno = panel.Document.Questions.ToList().FindIndex(q => q.Kind == ReviewQuestionKind.YesNo);

        panel.GoTo(yesno);

        var id = panel.Document.Questions[yesno].Id;

        panel.Press(Key.Enter);

        Assert.Equal(yesno, panel.Step);

        panel.Press(Key.N);

        Assert.Equal("No", view.Answers.Get(id));

        panel.Press(Key.Y);

        Assert.Equal("Yes", view.Answers.Get(id));

        Assert.True(panel.Press(Key.Enter));
        Assert.Equal(yesno + 1, panel.Step);

        Assert.True(panel.Press(Key.Backspace));
        Assert.Equal(yesno, panel.Step);

        var rating = panel.Document.Questions.ToList().FindIndex(q => q.Kind == ReviewQuestionKind.Rating);

        panel.GoTo(rating);
        panel.Press(Key.Digit4);

        Assert.Equal("4", view.Answers.Get(panel.Document.Questions[rating].Id));
    }

    [Fact]
    public void History_gives_the_round_and_what_was_said_last_time()
    {
        var folder = Path.Combine(Path.GetTempPath(), "silo-review-test-" + Guid.NewGuid().ToString("N"));

        Assert.Equal(1, ReviewHistory.Load(folder, "A", "abc1234 Subject").Round);

        var first = new ReviewAnswers("A");

        first.Set("q", "No", "broken");
        first.Save(folder, new DateTimeOffset(2026, 11, 27, 18, 0, 0, TimeSpan.Zero));

        var history = ReviewHistory.Load(folder, "A", "abc1234 Subject");

        Assert.Equal(2, history.Round);
        Assert.Equal("No: broken", history.Said("q"));
        Assert.Null(history.Said("other"));
        Assert.Equal(1, ReviewHistory.Load(folder, "B", null).Round);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void The_step_shows_what_was_said_last_time()
    {
        var folder = Path.Combine(Path.GetTempPath(), "silo-review-test-" + Guid.NewGuid().ToString("N"));
        var document = ReviewMode.Load("ToolCallCard", null);
        var first = new ReviewAnswers("ToolCallCard");

        first.Set(document.Questions[0].Id, "2", null);
        first.Save(folder, DateTimeOffset.UnixEpoch);

        var view = new ReviewView(document, ReviewHistory.Load(folder, "ToolCallCard", null));
        var texts = Find<Box>(view.Panel, $"question-{document.Questions[0].Id}").Children.OfType<TextArea>().Select(a => a.Buffer.Text);

        Assert.Contains("Last time you said: 2", texts);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void A_Control_that_is_not_in_the_catalog_is_an_ArgumentException()
        => Assert.Throws<ArgumentException>(() => new ReviewView(ReviewMode.Load("Nothing", null)));
}
