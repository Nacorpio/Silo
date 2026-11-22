namespace Silo;

/// <summary>A header that shows or hides a piece of content each time it is clicked.</summary>
/// <remarks>
/// <para>
/// An Expander stacks its <see cref="HeaderControl"/> above its <see cref="Content"/>. While it is open it has the
/// <c>expanded</c> Pseudo-state, so a Stylesheet can restyle it with <c>Expander:expanded</c>. While it is closed the content
/// is not part of the tree, so it takes no space and is not drawn.
/// </para>
/// <para>The Default theme gives the Expander a column layout. Style the header with the <c>ExpanderHeader</c> type.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var advanced = new Expander { Header = "Advanced", Content = new TextBlock { Text = "More options" } };
///
/// advanced.ExpandedChanged += (_, _) => settings.ShowAdvanced = advanced.IsExpanded;
/// advanced.IsExpanded = true;
/// </code>
/// </example>
public class Expander : Control
{
    /// <summary>The header that is clicked.</summary>
    readonly ExpanderHeader header = new();

    /// <summary>The content.</summary>
    Control? content;

    /// <summary>Whether the content is shown.</summary>
    bool isExpanded;

    /// <summary>Creates a closed Expander.</summary>
    public Expander()
    {
        header.Click += (_, _) => IsExpanded = !isExpanded;

        Children.Add(header);
    }

    /// <summary>Raised after <see cref="IsExpanded"/> changed.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Gets or sets the title in the header.</summary>
    public string Header
    {
        get => header.Text;
        set => header.Text = value;
    }

    /// <summary>Gets the header, so that code can add a Class to it or listen to it.</summary>
    public ExpanderHeader HeaderControl => header;

    /// <summary>Gets or sets what is shown while the Expander is open, or <see langword="null"/> for nothing.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            if (content == value)
                return;

            if (content is not null)
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

            SetPseudoState("expanded", value);

            header.IsExpanded = value;

            if (content is not null && value)
                Children.Add(content);

            if (content is not null && !value)
                Children.Remove(content);

            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
