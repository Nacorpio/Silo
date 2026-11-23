namespace Silo;

/// <summary>A bordered frame with a header above its content.</summary>
/// <remarks>
/// <para>
/// A GroupBox stacks its <see cref="HeaderControl"/> above its <see cref="Content"/>. Both are plain Controls: style the
/// header with the Class <c>header</c>, for example <c>GroupBox .header</c>. An empty <see cref="Header"/> is not shown.
/// </para>
/// <para>The Default theme gives the GroupBox a column layout, a border and a padding.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var options = new GroupBox { Header = "Options", Content = new CheckBox { Text = "Wrap lines" } };
/// </code>
/// </example>
public class GroupBox : Control
{
    /// <summary>The header.</summary>
    readonly TextBlock header = new();


    /// <summary>The content.</summary>
    Control? content;


    /// <summary>
    /// Runs before the first GroupBox is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static GroupBox()
    {
    }

    /// <summary>Creates an empty GroupBox.</summary>
    public GroupBox() => header.AddClass("header");


    /// <summary>Gets or sets the title. An empty title shows no header.</summary>
    public string Header
    {
        get => header.Text;
        set
        {
            header.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets the header, so that code can add a Class to it.</summary>
    public TextBlock HeaderControl => header;

    /// <summary>Gets or sets what the GroupBox frames, or <see langword="null"/> for nothing.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;

            Rebuild();
        }
    }

    /// <summary>Puts the parts that are in use into the GroupBox, in order.</summary>
    void Rebuild()
    {
        Children.Clear();

        if (header.Text.Length > 0)
            Children.Add(header);

        if (content is not null)
            Children.Add(content);
    }
}
