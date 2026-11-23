namespace Silo;

/// <summary>A padded surface with optional header and footer content around its main content.</summary>
/// <remarks>
/// <para>
/// A Card stacks its <see cref="Header"/>, <see cref="Content"/> and <see cref="Footer"/>, each of which is optional and any
/// Control, so a header can hold a title and a button. The Card gives the parts the Classes <c>header</c>, <c>content</c> and
/// <c>footer</c>, so a Stylesheet can restyle them with <c>Card .footer</c>.
/// </para>
/// <para>The Default theme gives the Card a column layout, a surface background, a border and a padding.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var card = new Card
/// {
///     Header = new TextBlock { Text = "Build #42" },
///     Content = new TextBlock { Text = "Passed in 3 minutes" },
///     Footer = new Button { Text = "Open" },
/// };
/// </code>
/// </example>
public class Card : Control
{
    /// <summary>The header.</summary>
    Control? header;

    /// <summary>The content.</summary>
    Control? content;

    /// <summary>The footer.</summary>
    Control? footer;


    /// <summary>
    /// Runs before the first Card is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Card()
    {
    }


    /// <summary>Gets or sets the Control above the content, or <see langword="null"/> for none.</summary>
    public Control? Header
    {
        get => header;
        set => Set(ref header, value, "header");
    }

    /// <summary>Gets or sets the main Control, or <see langword="null"/> for none.</summary>
    public Control? Content
    {
        get => content;
        set => Set(ref content, value, "content");
    }

    /// <summary>Gets or sets the Control below the content, or <see langword="null"/> for none.</summary>
    public Control? Footer
    {
        get => footer;
        set => Set(ref footer, value, "footer");
    }

    /// <summary>Replaces one part and puts the parts into the Card in order.</summary>
    /// <param name="field">The field of the part.</param>
    /// <param name="value">The new Control.</param>
    /// <param name="className">The Class that marks the part.</param>
    void Set(ref Control? field, Control? value, string className)
    {
        field?.RemoveClass(className);

        field = value;

        value?.AddClass(className);

        Children.Clear();

        foreach (var part in new[] { header, content, footer })
            if (part is not null)
                Children.Add(part);
    }
}
