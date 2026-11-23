namespace Silo;

/// <summary>A column of <see cref="Expander"/>s of which one, or any number, can be open.</summary>
/// <remarks>
/// <para>
/// <see cref="Add"/> creates an Expander. While <see cref="AllowMultiple"/> is <see langword="false"/> (the default), opening
/// one Expander closes the others, so at most one is open. The Expanders keep their own styles, and the Accordion is a column
/// with a gap, so style them with <c>Accordion Expander</c>.
/// </para>
/// <para>Apply styles again after an Expander opens or closes.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var faq = new Accordion();
///
/// faq.Add("What is Silo?", new TextBlock { Text = "A UI library." });
/// faq.Add("Is it free?", new TextBlock { Text = "Yes." });
/// faq.ExpandedIndex = 0;
/// </code>
/// </example>
public class Accordion : Control
{
    /// <summary>The Expanders in order.</summary>
    readonly List<Expander> sections = [];


    /// <summary>Whether a change is being made by the Accordion itself.</summary>
    bool changing;

    /// <summary>Whether more than one Expander can be open.</summary>
    bool allowMultiple;


    /// <summary>
    /// Runs before the first Accordion is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Accordion()
    {
    }

    /// <summary>Raised after an Expander opened or closed.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Gets the Expanders in order.</summary>
    public IReadOnlyList<Expander> Sections => sections;

    /// <summary>
    /// Gets or sets whether several Expanders can be open at once. Turning it off closes all but the first open one.
    /// </summary>
    public bool AllowMultiple
    {
        get => allowMultiple;
        set
        {
            allowMultiple = value;

            if (value)
                return;

            var keep = sections.FindIndex(section => section.IsExpanded);

            CloseOthers(keep >= 0 ? sections[keep] : null);
        }
    }

    /// <summary>Gets or sets the index of the first open Expander, or -1 when none is open. Setting it opens that one and closes the others, whatever <see cref="AllowMultiple"/> is.</summary>
    public int ExpandedIndex
    {
        get => sections.FindIndex(section => section.IsExpanded);
        set
        {
            for (var i = 0; i < sections.Count; i++)
                sections[i].IsExpanded = i == value;
        }
    }

    /// <summary>Adds an Expander.</summary>
    /// <param name="header">The title of the Expander.</param>
    /// <param name="content">What it shows while open.</param>
    /// <returns>The Expander, so that code can add a Class to it.</returns>
    public Expander Add(string header, Control content)
    {
        var section = new Expander { Header = header, Content = content };

        section.ExpandedChanged += (_, _) =>
        {
            if (changing)
                return;

            if (section.IsExpanded && !allowMultiple)
                CloseOthers(section);

            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        };

        sections.Add(section);
        Children.Add(section);

        return section;
    }

    /// <summary>Closes every Expander but one.</summary>
    /// <param name="keep">The Expander to leave as it is, or <see langword="null"/> to close all.</param>
    void CloseOthers(Expander? keep)
    {
        changing = true;

        foreach (var section in sections.Where(section => section != keep))
            section.IsExpanded = false;

        changing = false;
    }
}
