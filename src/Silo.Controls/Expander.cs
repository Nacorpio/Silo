namespace Silo;

/// <summary>
/// A header that the user presses to show or hide a Control below it.
/// </summary>
/// <remarks>
/// <para>
/// While the content is shown the Expander has the <c>expanded</c> Pseudo-state, so a Stylesheet can restyle it with
/// <c>Expander:expanded</c>. The header is a <see cref="Button"/> with the Class <c>expander-header</c>. It starts with
/// <c>&gt;</c> while the content is hidden and <c>v</c> while it is shown. The content is removed from the Expander's
/// <see cref="Element.Children"/> while it is hidden, so it takes no room.
/// </para>
/// <para>Styles must be applied again after <see cref="IsExpanded"/> changes.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var advanced = new Expander { Header = "Advanced", Content = new TextBlock { Text = "More options" } };
///
/// advanced.IsExpanded = true;
/// </code>
/// </example>
public class Expander : Box
{
    /// <summary>The header Button.</summary>
    readonly Button header = new();

    /// <summary>The text of the header without the arrow.</summary>
    string headerText = "";

    /// <summary>The Control that is shown or hidden, or <see langword="null"/>.</summary>
    Control? content;

    /// <summary>Whether the content is shown.</summary>
    bool isExpanded;


    /// <summary>Creates an Expander with a header and no content.</summary>
    public Expander()
    {
        header.AddClass("expander-header");
        header.Click += (_, _) => IsExpanded = !isExpanded;

        Children.Add(header);
        UpdateHeader();
    }

    /// <summary>Raised after <see cref="IsExpanded"/> changed.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Gets or sets the text of the header.</summary>
    public string Header
    {
        get => headerText;
        set
        {
            headerText = value ?? "";

            UpdateHeader();
        }
    }

    /// <summary>Gets or sets the Control that the header shows and hides. It is not part of the tree until the Expander is expanded.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            if (content == value)
                return;

            if (content is not null && content.Parent == this)
                Children.Remove(content);

            content = value;

            if (isExpanded && content is not null)
                Children.Add(content);
        }
    }

    /// <summary>Gets or sets whether the content is shown. Styles must be applied again after it changes.</summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
                return;

            isExpanded = value;

            if (content is not null)
            {
                if (value)
                    Children.Add(content);
                else
                    Children.Remove(content);
            }

            SetPseudoState("expanded", value);
            UpdateHeader();

            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Puts the arrow and the text into the header.</summary>
    void UpdateHeader() => header.Text = (isExpanded ? "v " : "> ") + headerText;
}
