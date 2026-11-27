using Silo.Styling;

namespace Silo.Studio;

/// <summary>
/// The review window: a split view with one live sample of a new Control on the left and the <see cref="ReviewPanel"/> on the
/// right. A picker for the variant and the Pseudo-state of the sample sits above it, so the user can try the Control in every
/// way that matters, then answer the questions.
/// </summary>
/// <remarks>
/// <para>
/// The view does not choose Stylesheets or write files by itself. It raises <see cref="VariantChanged"/> when the user picks
/// Light or Dark, and <see cref="Submitted"/> when they press Submit, and the host does the rest.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var view = new ReviewView(document);
///
/// view.Submitted += (_, _) => view.Panel.ShowStatus("Saved to " + view.Answers.Save("reviews", DateTimeOffset.Now));
/// </code>
/// </example>
public sealed class ReviewView : Window
{
    /// <summary>The samples that the Control can be shown as.</summary>
    readonly IReadOnlyList<(string Name, Func<Control> Create)> variants;

    /// <summary>The picker for the variant.</summary>
    readonly SegmentedControl variantPicker = new() { Name = "review-variants" };

    /// <summary>The picker for the Pseudo-state.</summary>
    readonly SegmentedControl statePicker = new() { Name = "review-states" };

    /// <summary>The box that holds the sample.</summary>
    readonly Box stage = new() { Name = "review-stage" };

    /// <summary>The button that picks the light variant.</summary>
    readonly Button light = new() { Text = "Light", Name = "light" };

    /// <summary>The button that picks the dark variant.</summary>
    readonly Button dark = new() { Text = "Dark", Name = "dark" };

    /// <summary>Whether the pickers are being set by code, so that their events are not answered.</summary>
    bool updating;

    /// <summary>The Pseudo-states of the shown sample.</summary>
    IReadOnlyList<string> states = [];

    /// <summary>The theme variant that the window is shown in.</summary>
    ThemeVariant variant;

    /// <summary>Creates the view with the first sample shown.</summary>
    /// <param name="document">The questions.</param>
    /// <exception cref="ArgumentException">The Control of the document is not in the <see cref="ControlCatalog"/>.</exception>
    /// <param name="history">What earlier rounds said, or <see langword="null"/> for a first round.</param>
    public ReviewView(ReviewDocument document, ReviewHistory? history = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        Name = "review-window";
        Title = $"Review: {document.Control}";
        InitialWidth = 1280;
        InitialHeight = 780;

        variants = ControlCatalog.VariantsOf(document.Control);
        Answers = new ReviewAnswers(document.Control);
        Panel = new ReviewPanel(document, Answers, history ?? ReviewHistory.None);

        var bar = new Box { Name = "bar" };
        var title = new TextBlock { Text = document.Control };
        var controls = new Box { Name = "review-controls" };
        var left = new Box { Name = "review-left" };
        var split = new SplitPane { Name = "review-split" };

        title.AddClass("title");

        variantPicker.Items = [.. variants.Select(v => v.Name)];
        variantPicker.SelectedIndex = 0;
        variantPicker.SelectionChanged += (_, _) => Picked();
        statePicker.SelectionChanged += (_, _) => Picked();

        light.Click += (_, _) => Variant = ThemeVariant.Light;
        dark.Click += (_, _) => Variant = ThemeVariant.Dark;

        bar.Children.Add(title);
        bar.Children.Add(light);
        bar.Children.Add(dark);

        if (variants.Count > 1)
            controls.Children.Add(variantPicker);

        controls.Children.Add(statePicker);

        left.Children.Add(controls);
        left.Children.Add(stage);

        split.AddPane(left, minSize: 360);
        split.AddPane(Panel, minSize: 340, size: 470);

        Children.Add(bar);
        Children.Add(split);

        Panel.Submitted += (_, _) => Submitted?.Invoke(this, EventArgs.Empty);
        Panel.ShowRequested += (_, question) => Show(question.Variant, question.State);

        ShowSelected();
        ShowSample();

        if (Panel.Current is { } first)
            Show(first.Variant, first.State);
    }

    /// <summary>Raised after <see cref="Variant"/> changed.</summary>
    public event EventHandler? VariantChanged;

    /// <summary>Raised when the user presses Submit.</summary>
    public event EventHandler? Submitted;

    /// <summary>Gets <c>Window</c>, so that the theme's Window Rules apply.</summary>
    public override string TypeName => "Window";

    /// <summary>Gets the panel with the questions.</summary>
    public ReviewPanel Panel { get; }

    /// <summary>Gets the answers so far.</summary>
    public ReviewAnswers Answers { get; }

    /// <summary>Gets the sample that is shown, or <see langword="null"/> before the first one.</summary>
    public Control? Sample { get; private set; }

    /// <summary>Gets the name of the variant that is shown.</summary>
    public string CurrentVariant => variants[Math.Max(0, variantPicker.SelectedIndex)].Name;

    /// <summary>Gets the Pseudo-state that is shown, <c>normal</c> for none.</summary>
    public string CurrentState => states.Count == 0 ? "normal" : states[Math.Max(0, statePicker.SelectedIndex)];

    /// <summary>Gets or sets the theme variant that the user has picked. The default is <see cref="ThemeVariant.Light"/>.</summary>
    public ThemeVariant Variant
    {
        get => variant;
        set
        {
            if (variant == value)
                return;

            variant = value;

            ShowSelected();

            VariantChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Switches the sample to a variant and a Pseudo-state.</summary>
    /// <param name="name">The name of the variant, or <see langword="null"/> to keep the current one.</param>
    /// <param name="state">The Pseudo-state, or <see langword="null"/> for <c>normal</c>.</param>
    /// <remarks>A name or a state that does not exist is ignored.</remarks>
    public void Show(string? name, string? state)
    {
        var index = name is null ? -1 : variants.ToList().FindIndex(v => v.Name == name);

        updating = true;

        if (index >= 0)
            variantPicker.SelectedIndex = index;

        updating = false;

        ShowSample(resetState: true);

        var stateIndex = states.ToList().IndexOf(state ?? "normal");

        if (stateIndex < 0)
            return;

        updating = true;
        statePicker.SelectedIndex = stateIndex;
        updating = false;

        ShowSample();
    }

    /// <summary>Creates the sample of the picked variant, puts it in the picked state and shows it.</summary>
    /// <param name="resetState">Whether to go back to <c>normal</c>.</param>
    void ShowSample(bool resetState = false)
    {
        var sample = variants[Math.Max(0, variantPicker.SelectedIndex)].Create();
        var available = ControlCatalog.StatesOf(sample.GetType());

        if (!available.SequenceEqual(states) || resetState)
        {
            updating = true;

            states = available;
            statePicker.Items = states;
            statePicker.SelectedIndex = 0;

            updating = false;
        }

        ThemeMatrix.Apply(sample, CurrentState);

        stage.Children.Clear();
        stage.Children.Add(sample);

        Sample = sample;

        Answers.MarkViewed(CurrentVariant, CurrentState);
    }

    /// <summary>Answers a change of a picker that the user made.</summary>
    void Picked()
    {
        if (!updating)
            ShowSample();
    }

    /// <summary>Marks the button of the current variant with the <c>selected</c> Class.</summary>
    void ShowSelected()
    {
        SetSelected(light, variant == ThemeVariant.Light);
        SetSelected(dark, variant == ThemeVariant.Dark);
    }

    /// <summary>Adds or removes the <c>selected</c> Class.</summary>
    /// <param name="button">The button.</param>
    /// <param name="selected">Whether it is the picked one.</param>
    static void SetSelected(Button button, bool selected)
    {
        _ = selected ? button.AddClass("selected") : button.RemoveClass("selected");
    }
}
