namespace Silo;

/// <summary>
/// A centered message that fills the place of content that is not there: an icon, a title, a line of explanation and an
/// optional action.
/// </summary>
/// <remarks>
/// <para>
/// Use it for an empty list, a search without results or a page that has nothing yet. Set <see cref="Glyph"/>,
/// <see cref="Title"/> and <see cref="Description"/> as needed; a part that is empty is left out. <see cref="Action"/> takes any
/// Control, normally a <see cref="Button"/> that leads out of the empty state.
/// </para>
/// <para>
/// The parts carry the classes <c>empty-icon</c>, <c>empty-title</c>, <c>empty-description</c> and <c>empty-action</c>, so a
/// Stylesheet can restyle each one. The EmptyState itself is a centered column.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var add = new Button { Text = "Add a file" };
///
/// var empty = new EmptyState
/// {
///     Glyph = "search",
///     Title = "No files yet",
///     Description = "Files you add show up here.",
///     Action = add,
/// };
/// </code>
/// </example>
public class EmptyState : Control
{
    readonly Icon icon = new();

    readonly TextBlock title = new();

    readonly TextBlock description = new();

    Control? action;

    /// <summary>Creates an EmptyState with no parts.</summary>
    public EmptyState()
    {
        icon.AddClass("empty-icon");
        title.AddClass("empty-title");
        description.AddClass("empty-description");
    }

    /// <summary>Gets or sets the name of the glyph shown on top. An empty name leaves the icon out.</summary>
    public string Glyph
    {
        get => icon.Glyph;
        set
        {
            icon.Glyph = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the headline. An empty text leaves it out.</summary>
    public string Title
    {
        get => title.Text;
        set
        {
            title.Text = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the line of explanation under the headline. An empty text leaves it out.</summary>
    public string Description
    {
        get => description.Text;
        set
        {
            description.Text = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the Control shown last, such as a Button, or <see langword="null"/> for none.</summary>
    public Control? Action
    {
        get => action;
        set
        {
            action?.RemoveClass("empty-action");

            action = value;
            action?.AddClass("empty-action");

            Rebuild();
        }
    }

    /// <summary>Puts the parts that have something to show in the tree, in order.</summary>
    void Rebuild()
    {
        Children.Clear();

        if (icon.Glyph.Length > 0)
            Children.Add(icon);

        if (title.Text.Length > 0)
            Children.Add(title);

        if (description.Text.Length > 0)
            Children.Add(description);

        if (action is not null)
            Children.Add(action);
    }
}
