namespace Silo.Studio;

/// <summary>
/// The right side of the review window, as a wizard: one question of a <see cref="ReviewDocument"/> at a time, then a summary with the
/// comment and Submit. It is built from Silo's own controls and writes what the user answers into a <see cref="ReviewAnswers"/>.
/// </summary>
/// <remarks>
/// <para>
/// Moving to a question that names a variant or a state raises <see cref="ShowRequested"/>, and the window switches the sample. A row
/// of numbered steps shows which questions have an answer, which were skipped and which one is current, and a step can be clicked.
/// Next works without an answer. The last step lists the questions that have none.
/// </para>
/// <para>
/// <see cref="Press"/> answers from the keyboard: 1 to 5 for stars or for the options of a question, Y, N and U for Yes, No and Not
/// sure, Enter for the next step once the question has an answer, Backspace for the previous one, and Left and Right for either when
/// no control has the keyboard. It does nothing while a text box has the keyboard.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var panel = new ReviewPanel(document, new ReviewAnswers(document.Control), ReviewHistory.None);
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

    /// <summary>What earlier rounds said.</summary>
    readonly ReviewHistory history;

    /// <summary>The text boxes and areas that the user types in.</summary>
    readonly List<Control> editors = [];

    /// <summary>The block of each question, built once so that what the user entered stays while they go back and forth.</summary>
    readonly List<Box> blocks = [];

    /// <summary>The control that gives each question its answer from the keyboard.</summary>
    readonly List<Action<Key>> keyAnswers = [];

    /// <summary>The questions that the user has been to.</summary>
    readonly HashSet<int> visited = [];

    /// <summary>The numbered buttons above the step.</summary>
    readonly Box steps = new() { Name = "review-steps" };

    /// <summary>The scroller that holds the current step.</summary>
    readonly ScrollViewer scroll = new() { Name = "review-scroll" };

    /// <summary>The line that says which step it is.</summary>
    readonly TextBlock progress = new() { Name = "review-progress" };

    /// <summary>The line that says what Submit did.</summary>
    readonly TextBlock status = new() { Name = "review-status" };

    /// <summary>The box that asks for the iteration to be discarded.</summary>
    readonly CheckBox redo = new() { Text = "Discard this iteration and redo it", Name = "review-redo" };

    /// <summary>The Submit button.</summary>
    readonly Button submit = new() { Text = "Submit review", Name = "review-submit" };

    /// <summary>Goes to the previous step.</summary>
    readonly Button back = new() { Text = "Back", Name = "review-back" };

    /// <summary>Goes to the next step.</summary>
    readonly Button next = new() { Text = "Next", Name = "review-next" };

    /// <summary>The row with Back, Next and the progress.</summary>
    readonly Box nav = new() { Name = "review-nav" };

    /// <summary>The summary step, built again every time it is shown.</summary>
    readonly Box summary = new() { Name = "review-summary" };

    /// <summary>The block for the comment on the whole Control.</summary>
    readonly Box comment;

    /// <summary>The current step.</summary>
    int step;

    /// <summary>Creates the panel at its first question.</summary>
    /// <param name="document">The questions.</param>
    /// <param name="answers">Where the answers go.</param>
    /// <param name="history">What earlier rounds said, or <see cref="ReviewHistory.None"/>.</param>
    public ReviewPanel(ReviewDocument document, ReviewAnswers answers, ReviewHistory history)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(history);

        this.answers = answers;
        this.history = history;

        Name = "review";
        Document = document;

        var title = new TextBlock { Text = document.Title };

        title.AddClass("title");
        progress.AddClass("muted");
        status.AddClass("muted");
        back.AddClass("nav");
        next.AddClass("nav");

        Children.Add(title);
        Children.Add(Wrapped(Header(document), "meta"));
        Children.Add(steps);
        Children.Add(scroll);

        foreach (var question in document.Questions)
            blocks.Add(Build(question));

        comment = BuildComment();

        redo.CheckedChanged += (_, _) =>
        {
            answers.Redo = redo.IsChecked;
            submit.Text = redo.IsChecked ? "Submit and redo" : "Submit review";
        };

        submit.Click += (_, _) => Submitted?.Invoke(this, EventArgs.Empty);
        back.Click += (_, _) => Back();
        next.Click += (_, _) => Next();

        Children.Add(nav);
        Children.Add(status);

        Show();
    }

    /// <summary>Raised when the user presses Submit.</summary>
    public event EventHandler? Submitted;

    /// <summary>Raised when a question that names a variant or a state comes up, so that the sample can follow.</summary>
    public event EventHandler<ReviewQuestion>? ShowRequested;

    /// <summary>Raised after the step changed.</summary>
    public event EventHandler? StepChanged;

    /// <summary>Gets the questions.</summary>
    public ReviewDocument Document { get; }

    /// <summary>Gets the box that asks for the iteration to be discarded and done again.</summary>
    public CheckBox RedoBox => redo;

    /// <summary>Gets the Submit button, which is in the summary step.</summary>
    public Button SubmitButton => submit;

    /// <summary>Gets the Back button.</summary>
    public Button BackButton => back;

    /// <summary>Gets the Next button.</summary>
    public Button NextButton => next;

    /// <summary>Gets the current step: the index of a question, or <see cref="SummaryStep"/>.</summary>
    public int Step => step;

    /// <summary>Gets the number of the summary step, which comes after the last question.</summary>
    public int SummaryStep => Document.Questions.Count;

    /// <summary>Gets whether the summary is shown.</summary>
    public bool IsSummary => step == SummaryStep;

    /// <summary>Gets the question that is shown, or <see langword="null"/> in the summary.</summary>
    public ReviewQuestion? Current => IsSummary ? null : Document.Questions[step];

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

    /// <summary>Shows a line under the step, such as where the answers were saved.</summary>
    /// <param name="text">The text.</param>
    public void ShowStatus(string text) => status.Text = text;

    /// <summary>Goes to a step.</summary>
    /// <param name="index">The step, from 0 to <see cref="SummaryStep"/>. A number outside that is ignored.</param>
    public void GoTo(int index)
    {
        if (index < 0 || index > SummaryStep || index == step)
            return;

        step = index;

        Show();

        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Goes to the next step.</summary>
    public void Next() => GoTo(step + 1);

    /// <summary>Goes to the previous step.</summary>
    public void Back() => GoTo(step - 1);

    /// <summary>Answers from the keyboard. See the remarks of the class for the keys.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    public bool Press(Key key)
    {
        if (editors.Any(e => e.PseudoStates.Contains("focus")))
            return false;

        var focused = HasFocus(this);

        switch (key)
        {
            case Key.Enter when !focused && !IsSummary && IsAnswered(step):
                Next();

                return true;

            case Key.Backspace:
                Back();

                return true;

            case Key.Right when !focused:
                Next();

                return true;

            case Key.Left when !focused:
                Back();

                return true;
        }

        if (IsSummary)
            return false;

        keyAnswers[step](key);

        return false;
    }

    /// <summary>Tells whether a question has an answer.</summary>
    /// <param name="index">The question.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    bool IsAnswered(int index) => answers.Get(Document.Questions[index].Id) is not null;

    /// <summary>Tells whether a control inside another one has the keyboard.</summary>
    /// <param name="root">The control to look in.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    static bool HasFocus(Element root)
        => root.PseudoStates.Contains("focus") || root.Children.Any(HasFocus);

    /// <summary>Gets the line under the title: the round, the commit that is reviewed and how to answer.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The text.</returns>
    string Header(ReviewDocument document)
    {
        var round = history.Round > 1 ? $"Round {history.Round}" : "Round 1";
        var commit = history.Commit is null ? string.Empty : $" · {history.Commit}";

        return document.Pr.Length > 0 ? $"{round}{commit} · {document.Pr}" : $"{round}{commit}";
    }

    /// <summary>Puts the current step on screen and updates the step buttons, the navigation and the sample.</summary>
    void Show()
    {
        visited.Add(step);

        RebuildSteps();

        if (IsSummary)
        {
            RebuildSummary();

            scroll.Content = summary;
        }
        else
        {
            scroll.Content = blocks[step];
        }

        nav.Children.Clear();

        if (step > 0)
            nav.Children.Add(back);

        if (!IsSummary)
            nav.Children.Add(next);

        nav.Children.Add(progress);

        next.Text = step == SummaryStep - 1 ? "Review" : "Next";
        progress.Text = IsSummary
            ? $"Summary · {answers.Count} of {Document.Questions.Count} answered"
            : $"{step + 1} of {Document.Questions.Count} · {answers.Count} answered";

        if (Current is { } question && (question.Variant is not null || question.State is not null))
            ShowRequested?.Invoke(this, question);
    }

    /// <summary>Builds the numbered buttons again, so that they show what has an answer.</summary>
    void RebuildSteps()
    {
        steps.Children.Clear();

        for (var i = 0; i <= SummaryStep; i++)
        {
            var index = i;
            var dot = new Button { Text = i == SummaryStep ? "Done" : (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) };

            dot.AddClass("dot");

            if (i == step)
                dot.AddClass("current");
            else if (i < SummaryStep && IsAnswered(i))
                dot.AddClass("answered");
            else if (visited.Contains(i) && i < SummaryStep)
                dot.AddClass("skipped");

            dot.Click += (_, _) => GoTo(index);

            steps.Children.Add(dot);
        }
    }

    /// <summary>Builds the summary: every question with its answer, the comment, the redo box and Submit.</summary>
    void RebuildSummary()
    {
        summary.Children.Clear();

        var title = new TextBlock { Text = "Summary" };
        var open = Document.Questions.Count - answers.Count;

        title.AddClass("prompt");
        summary.Children.Add(title);
        summary.Children.Add(Muted(open == 0 ? "Every question has an answer." : $"{open} without an answer. Click a number above to go back to it."));

        foreach (var (question, index) in Document.Questions.Select((q, i) => (q, i)))
        {
            var answer = answers.Get(question.Id);
            summary.Children.Add(Wrapped($"{index + 1}. {question.Prompt} — {answer ?? "no answer"}", answer is null ? "unanswered" : "answer"));
        }

        summary.Children.Add(comment);
        summary.Children.Add(redo);
        summary.Children.Add(submit);
    }

    /// <summary>Builds the block of one question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The block.</returns>
    Box Build(ReviewQuestion question)
    {
        var block = new Box { Name = $"question-{question.Id}" };
        var last = history.Said(question.Id);

        block.AddClass("question");
        block.Children.Add(Wrapped(question.Prompt, "prompt"));

        if (question.Hint.Length > 0)
            block.Children.Add(Wrapped(question.Hint, "hint"));

        if (last is not null)
            block.Children.Add(Wrapped($"Last time you said: {last}", "last"));

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
                keyAnswers.Add(_ => { });
                break;
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

            RefreshProgress();
        };

        keyAnswers.Add(key =>
        {
            if (key is >= Key.Digit1 and <= Key.Digit5)
                rating.Value = key - Key.Digit0;
        });

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

            RefreshProgress();
        }

        choice.SelectionChanged += (_, _) => Store();
        note.TextChanged += (_, _) => Store();

        keyAnswers.Add(key =>
        {
            var index = key switch
            {
                Key.Y or Key.Digit1 => 0,
                Key.N or Key.Digit2 => 1,
                Key.U or Key.Digit3 => 2,
                _ => -1,
            };

            if (index >= 0)
                choice.SelectedIndex = index;
        });

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

            RefreshProgress();
        };

        keyAnswers.Add(key =>
        {
            var index = key - Key.Digit1;

            if (index >= 0 && index < question.Options.Count)
                group.SelectedIndex = index;
        });

        return group;
    }

    /// <summary>Builds the text area of a text question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The control.</returns>
    TextArea BuildText(ReviewQuestion question)
    {
        var area = new TextArea { Name = $"text-{question.Id}" };

        area.AddClass("entry");

        area.TextChanged += (_, _) =>
        {
            answers.Set(question.Id, area.Buffer.Text.Trim(), null);

            RefreshProgress();
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
        area.AddClass("entry");

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

    /// <summary>Creates text that wraps and can be selected, which a <see cref="TextBlock"/> cannot do yet.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cssClass">The Class that styles it.</param>
    /// <returns>The area.</returns>
    static TextArea Wrapped(string text, string cssClass)
    {
        var area = new TextArea(new TextBuffer(text)) { IsReadOnly = true, WordWrap = true, MinimumLines = 1, MaximumLines = int.MaxValue };

        area.AddClass("wrapped");
        area.AddClass(cssClass);

        return area;
    }

    /// <summary>Updates the step buttons and the count after an answer changed.</summary>
    void RefreshProgress()
    {
        RebuildSteps();

        progress.Text = IsSummary
            ? $"Summary · {answers.Count} of {Document.Questions.Count} answered"
            : $"{step + 1} of {Document.Questions.Count} · {answers.Count} answered";
    }
}
