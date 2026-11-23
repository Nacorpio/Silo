namespace Silo;

/// <summary>A header of numbered steps above the content of the current one, for a wizard.</summary>
/// <remarks>
/// <para>
/// Each step in the header is a row with the Class <c>step</c> and one of <c>done</c>, <c>current</c> or <c>upcoming</c>. It
/// holds a TextBlock <c>step-number</c> and a TextBlock <c>step-title</c>; a Box <c>step-line</c> joins it to the next step.
/// The header has the Class <c>steps</c> and the area for the content <c>step-content</c>.
/// </para>
/// <para>
/// <see cref="Next"/> and <see cref="Back"/> move between the steps. The Stepper does not validate: the application decides
/// when <see cref="Next"/> may run, for example from the click of its own button. Apply styles again after the step changes.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var wizard = new Stepper { Steps = [new("Account", accountForm), new("Plan", planForm), new("Done", summary)] };
///
/// next.Click += (_, _) =>
/// {
///     if (accountForm.IsValid)
///         wizard.Next();
/// };
/// </code>
/// </example>
public class Stepper : Control
{
    /// <summary>The header.</summary>
    readonly Box header = new();

    /// <summary>The area for the content of the current step.</summary>
    readonly Box body = new();


    /// <summary>The steps.</summary>
    IReadOnlyList<WizardStep> steps = [];

    /// <summary>The index of the current step.</summary>
    int currentStep;


    /// <summary>
    /// Runs before the first Stepper is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Stepper()
    {
    }

    /// <summary>Creates a Stepper with no steps.</summary>
    public Stepper()
    {
        header.AddClass("steps");
        body.AddClass("step-content");

        Children.Add(header);
        Children.Add(body);
    }

    /// <summary>Raised after <see cref="CurrentStep"/> changed.</summary>
    public event EventHandler? StepChanged;

    /// <summary>Gets or sets the steps. Setting it goes back to the first step.</summary>
    public IReadOnlyList<WizardStep> Steps
    {
        get => steps;
        set
        {
            steps = value ?? [];
            currentStep = 0;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the index of the current step. A value outside the steps is moved into range.</summary>
    public int CurrentStep
    {
        get => currentStep;
        set
        {
            var next = Math.Clamp(value, 0, Math.Max(0, steps.Count - 1));

            if (next == currentStep)
                return;

            currentStep = next;

            Rebuild();

            StepChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets whether the current step is the first.</summary>
    public bool IsFirst => currentStep == 0;

    /// <summary>Gets whether the current step is the last.</summary>
    public bool IsLast => currentStep >= steps.Count - 1;

    /// <summary>Goes to the next step.</summary>
    /// <returns><see langword="false"/> when the current step is the last.</returns>
    public bool Next()
    {
        if (IsLast)
            return false;

        CurrentStep++;

        return true;
    }

    /// <summary>Goes to the previous step.</summary>
    /// <returns><see langword="false"/> when the current step is the first.</returns>
    public bool Back()
    {
        if (IsFirst)
            return false;

        CurrentStep--;

        return true;
    }

    /// <summary>Builds the header and shows the content of the current step.</summary>
    void Rebuild()
    {
        header.Children.Clear();
        body.Children.Clear();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = new Box();
            var number = new TextBlock { Text = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) };
            var title = new TextBlock { Text = steps[i].Title };

            step.AddClass("step");
            step.AddClass(i < currentStep ? "done" : i == currentStep ? "current" : "upcoming");
            number.AddClass("step-number");
            title.AddClass("step-title");

            step.Children.Add(number);
            step.Children.Add(title);
            header.Children.Add(step);

            if (i == steps.Count - 1)
                continue;

            var line = new Box();

            line.AddClass("step-line");

            if (i < currentStep)
                line.AddClass("done");

            header.Children.Add(line);
        }

        if (steps.Count > 0)
            body.Children.Add(steps[currentStep].Content);
    }
}
