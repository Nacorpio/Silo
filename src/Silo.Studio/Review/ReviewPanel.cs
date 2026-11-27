namespace Silo.Studio;

/// <summary>
/// The right side of the review window: the questions of a <see cref="ReviewDocument"/>, a comment box and a Submit button. It
/// is built from Silo's own controls and writes what the user answers into a <see cref="ReviewAnswers"/>.
/// </summary>
/// <remarks>
/// <para>
/// A question that names a variant or a state has a Show button. The panel does not show the sample itself. It raises
/// <see cref="ShowRequested"/> and the window switches the sample.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var panel = new ReviewPanel(document, new ReviewAnswers(document.Control));
///
/// panel.Submitted += (_, _) => Console.WriteLine("Submit pressed");
/// </code>
/// </example>
public sealed class ReviewPanel : Box
{
    /// <summary>The options of a yes or no question.</summary>
    static readonly string[] YesNo = ["Yes", "No", "Not sure"];

    /// <summary>The answers that the panel fills.</summary>
    readonly ReviewAnswers answers;

    /// <summary>The text boxes and areas that need a clipboard.</summary>
    readonly List<Control> editors = [];

    /// <summary>The line that says how many questions have an answer.</summary>
    readonly TextBlock progress = new() { Name = "review-progress" };

    /// <summary>The line that says what Submit did.</summary>
    readonly TextBlock status = new() { Name = "review-status" };

    /// <summary>The box that asks for the iteration to be discarded.</summary>
    readonly CheckBox redo = new() { Text = "Discard this iteration and redo it", Name = "review-redo" };

    /// <summary>The Submit button.</summary>
    readonly Button submit = new() { Text = "Submit review", Name = "review-submit" };

    /// <summary>Creates the panel.</summary>
    /// <param name="document">The questions.</param>
    /// <param name="answers">Where the answers go.</param>
    public ReviewPanel(ReviewDocument document, ReviewAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(answers);

        this.answers = answers;

        Name = "review";
        Document = document;

        var title = new TextBlock { Text = document.Title };
        var list = new Box { Name = "review-questions" };
        var scroll = new ScrollViewer { Name = "review-scroll", Content = list };
        var footer = new Box { Name = "review-footer" };

        title.AddClass("title");
        progress.AddClass("muted");
        status.AddClass("muted");

        Children.Add(title);

        if (document.Pr.Length > 0)
            Children.Add(Muted(document.Pr));

        foreach (var question in document.Questions)
            list.Children.Add(Build(question));

        list.Children.Add(BuildComment());

        redo.CheckedChanged += (_, _) =>
        {
            answers.Redo = redo.IsChecked;
            submit.Text = redo.IsChecked ? "Submit and redo" : "Submit review";
        };

        submit.Click += (_, _) => Submitted?.Invoke(this, EventArgs.Empty);

        footer.Children.Add(submit);
        footer.Children.Add(progress);

        Children.Add(scroll);
        Children.Add(redo);
        Children.Add(status);
        Children.Add(footer);

        ShowProgress();
    }

    /// <summary>Raised when the user presses Submit.</summary>
    public event EventHandler? Submitted;

    /// <summary>Raised when the user asks to see what a question is about.</summary>
    public event EventHandler<ReviewQuestion>? ShowRequested;

    /// <summary>Gets the questions.</summary>
    public ReviewDocument Document { get; }

    /// <summary>Gets the box that asks for the iteration to be discarded and done again.</summary>
    public CheckBox RedoBox => redo;

    /// <summary>Gets the Submit button.</summary>
    public Button SubmitButton => submit;

    /// <summary>Sets the clipboard of the text boxes, so that the user can paste into them.</summary>
    /// <param name="clipboard">The clipboard.</param>
    public void UseClipboard(IClipboard clipboard)
    {
        foreach (var editor in editors)
        {
            if (editor is TextBox box)
                box.Clipboard = clipboard;
            else if (editor is TextArea area)
                area.Clipboard = clipboard;
        }
    }

    /// <summary>Shows a line under the questions, such as where the answers were saved.</summary>
    /// <param name="text">The text.</param>
    public void ShowStatus(string text) => status.Text = text;

    /// <summary>Builds the block of one question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The block.</returns>
    Box Build(ReviewQuestion question)
    {
        var block = new Box { Name = $"question-{question.Id}" };
        var prompt = new TextBlock { Text = question.Prompt };

        block.AddClass("question");
        prompt.AddClass("prompt");

        block.Children.Add(prompt);

        if (question.Hint.Length > 0)
            block.Children.Add(Muted(question.Hint));

        switch (question.Kind)
        {
            case ReviewQuestionKind.Rating:
                block.Children.Add(BuildRating(question));
                break;

            case ReviewQuestionKind.YesNo:
                BuildYesNo(question, block);
                break;

            case ReviewQuestionKind.Choice:
                block.Children.Add(BuildChoice(question));
                break;

            default:
                block.Children.Add(BuildText(question));
                break;
        }

        if (question.Variant is not null || question.State is not null)
        {
            var show = new Button { Text = "Show this" };

            show.AddClass("show");
            show.Click += (_, _) => ShowRequested?.Invoke(this, question);

            block.Children.Add(show);
        }

        return block;
    }

    /// <summary>Builds the stars of a rating question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The control.</returns>
    Rating BuildRating(ReviewQuestion question)
    {
        var rating = new Rating { Maximum = 5, AllowClear = true };

        rating.ValueChanged += (_, _) =>
        {
            answers.Set(question.Id, rating.Value > 0 ? ((int)rating.Value).ToString() : string.Empty, null);

            ShowProgress();
        };

        return rating;
    }

    /// <summary>Builds the choice and the note of a yes or no question.</summary>
    /// <param name="question">The question.</param>
    /// <param name="block">The block to add them to.</param>
    void BuildYesNo(ReviewQuestion question, Box block)
    {
        var choice = new SegmentedControl { Items = YesNo, SelectedIndex = -1 };
        var note = new TextBox { Placeholder = "Note (optional)" };

        void Store()
        {
            answers.Set(question.Id, choice.SelectedIndex >= 0 ? YesNo[choice.SelectedIndex] : string.Empty, note.Text);

            ShowProgress();
        }

        choice.SelectionChanged += (_, _) => Store();
        note.TextChanged += (_, _) => Store();

        editors.Add(note);
        block.Children.Add(choice);
        block.Children.Add(note);
    }

    /// <summary>Builds the radio buttons of a choice question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The control.</returns>
    RadioGroup BuildChoice(ReviewQuestion question)
    {
        var group = new RadioGroup();

        foreach (var option in question.Options)
            group.Add(option);

        group.SelectionChanged += (_, _) =>
        {
            answers.Set(question.Id, group.SelectedIndex >= 0 ? question.Options[group.SelectedIndex] : string.Empty, null);

            ShowProgress();
        };

        return group;
    }

    /// <summary>Builds the text area of a text question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The control.</returns>
    TextArea BuildText(ReviewQuestion question)
    {
        var area = new TextArea { Name = $"text-{question.Id}" };

        area.TextChanged += (_, _) =>
        {
            answers.Set(question.Id, area.Buffer.Text.Trim(), null);

            ShowProgress();
        };

        editors.Add(area);

        return area;
    }

    /// <summary>Builds the block for the comment on the whole Control.</summary>
    /// <returns>The block.</returns>
    Box BuildComment()
    {
        var block = new Box { Name = "review-comment" };
        var prompt = new TextBlock { Text = "Anything else you noticed?" };
        var area = new TextArea { Name = "comment-text" };

        block.AddClass("question");
        prompt.AddClass("prompt");

        area.TextChanged += (_, _) => answers.Comment = area.Buffer.Text.Trim();

        editors.Add(area);
        block.Children.Add(prompt);
        block.Children.Add(area);

        return block;
    }

    /// <summary>Creates a muted line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The line.</returns>
    static TextBlock Muted(string text)
    {
        var line = new TextBlock { Text = text };

        line.AddClass("muted");

        return line;
    }

    /// <summary>Updates the count of answered questions.</summary>
    void ShowProgress() => progress.Text = $"{answers.Count} of {Document.Questions.Count} answered";
}
