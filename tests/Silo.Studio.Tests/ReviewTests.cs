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
    public void Show_this_on_a_question_switches_the_sample_and_the_panel_records_answers()
    {
        var view = new ReviewView(ReviewMode.Load("ToolCallCard", null));
        var show = Find<Box>(view.Panel, "question-status").Children.OfType<Button>().Single(b => b.HasClass("show"));

        show.PointerDown();
        show.PointerUp(true);

        Assert.Equal("Running", view.CurrentVariant);

        var rating = Find<Box>(view.Panel, "question-look").Children.OfType<Rating>().Single();

        rating.Value = 4;

        Assert.Equal("4", view.Answers.Get("look"));

        rating.Value = 0;

        Assert.Null(view.Answers.Get("look"));

        var comment = Find<TextArea>(view.Panel, "comment-text");

        comment.TextInput("note");

        Assert.Equal("note", view.Answers.Comment);
    }

    [Fact]
    public void Submit_raises_Submitted_on_the_panel_and_the_view()
    {
        var view = new ReviewView(ReviewMode.Load("ToolCallTimeline", null));
        var raised = 0;

        view.Submitted += (_, _) => raised++;

        view.Panel.SubmitButton.PointerDown();
        view.Panel.SubmitButton.PointerUp(true);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_Control_that_is_not_in_the_catalog_is_an_ArgumentException()
        => Assert.Throws<ArgumentException>(() => new ReviewView(ReviewMode.Load("Nothing", null)));
}
