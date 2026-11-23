namespace Silo;

/// <summary>An input with a label above it and, below it, a help text or an error message.</summary>
/// <remarks>
/// <para>
/// A FormField stacks a <see cref="Silo.Label"/> (the text <see cref="Label"/>, which names the <see cref="Content"/> so that a
/// press on it focuses the input), the <see cref="Content"/>, and a line of text: the <see cref="Error"/> if there is one,
/// otherwise the <see cref="HelpText"/>. There is no validation here: the application decides when a value is wrong and sets
/// <see cref="Error"/>.
/// </para>
/// <para>
/// While the error is set the FormField has the Class <c>invalid</c>, so a Stylesheet can restyle the input, for example
/// <c>FormField.invalid TextBox</c>. The parts have the Classes <c>label</c>, <c>help</c> and <c>error</c>. Parts without text are
/// left out. Apply styles again after any change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var age = new FormField { Label = "Age", Content = new NumberBox(), HelpText = "In years", IsRequired = true };
///
/// age.Error = IsAdult(ageBox.Value) ? string.Empty : "Must be an adult";
/// </code>
/// </example>
public class FormField : Control
{
    /// <summary>The label.</summary>
    readonly Silo.Label labelText = new();

    /// <summary>The help text.</summary>
    readonly TextBlock helpText = new();

    /// <summary>The error message.</summary>
    readonly ValidationMessage errorText = new();


    /// <summary>The input.</summary>
    Control? content;


    /// <summary>
    /// Runs before the first FormField is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static FormField()
    {
    }

    /// <summary>Creates an empty FormField.</summary>
    public FormField()
    {
        labelText.AddClass("label");
        helpText.AddClass("help");
        errorText.AddClass("error");

        Rebuild();
    }

    /// <summary>Gets or sets the text of the label. An empty text shows no label.</summary>
    public string Label
    {
        get => labelText.Text;
        set
        {
            labelText.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets or sets whether the label has the marker of a required input.</summary>
    public bool IsRequired
    {
        get => labelText.Classes.Contains("required");
        set
        {
            if (value)
                labelText.AddClass("required");
            else
                labelText.RemoveClass("required");
        }
    }

    /// <summary>Gets or sets the input, or <see langword="null"/> for none. The label names it.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;
            labelText.For = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the text below the input that explains it. It is hidden while there is an <see cref="Error"/>.</summary>
    public string HelpText
    {
        get => helpText.Text;
        set
        {
            helpText.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the error message. A non-empty text shows it instead of the help text and sets the Class <c>invalid</c>.</summary>
    public string Error
    {
        get => errorText.Text;
        set
        {
            errorText.Text = value ?? string.Empty;

            if (errorText.Text.Length > 0)
                AddClass("invalid");
            else
                RemoveClass("invalid");

            Rebuild();
        }
    }

    /// <summary>Puts the parts that are in use into the FormField, in order.</summary>
    void Rebuild()
    {
        Children.Clear();

        if (labelText.Text.Length > 0)
            Children.Add(labelText);

        if (content is not null)
            Children.Add(content);

        if (errorText.Text.Length > 0)
            Children.Add(errorText);
        else if (helpText.Text.Length > 0)
            Children.Add(helpText);
    }
}
