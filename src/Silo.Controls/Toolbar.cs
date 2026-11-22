namespace Silo;

/// <summary>
/// A row of commands: flat buttons, toggles and other Controls, with dividers between groups.
/// </summary>
/// <remarks>
/// <para>
/// The Toolbar is a row that the theme gives a background, a gap and a bottom border. The Default theme restyles the
/// <see cref="Button"/>s and <see cref="ToggleButton"/>s inside it as flat: they show no background until the pointer is over
/// them, and a checked toggle shows the <c>track</c> color. Any Control can be added, such as a <see cref="ComboBox"/>.
/// </para>
/// <para>Overflow into a "more" menu is not done yet; the Toolbar clips what does not fit.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var toolbar = new Toolbar();
///
/// toolbar.AddButton("Cut", () => editor.Cut());
/// toolbar.AddButton("Copy", () => editor.Copy());
/// toolbar.AddSeparator();
/// toolbar.AddButton("Paste", () => editor.Paste());
/// </code>
/// </example>
public class Toolbar : Control
{
    /// <summary>Adds a Control at the end of the Toolbar.</summary>
    /// <param name="control">The Control.</param>
    /// <returns>The same Control.</returns>
    public T Add<T>(T control) where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);

        Children.Add(control);

        return control;
    }

    /// <summary>Adds a flat button at the end of the Toolbar.</summary>
    /// <param name="text">The text of the button.</param>
    /// <param name="click">What to do when it is clicked. It may be <see langword="null"/>.</param>
    /// <returns>The button.</returns>
    public Button AddButton(string text, Action? click = null)
    {
        var button = new Button { Text = text };

        if (click is not null)
            button.Click += (_, _) => click();

        return Add(button);
    }

    /// <summary>Adds a vertical divider at the end of the Toolbar.</summary>
    /// <returns>The divider.</returns>
    public Separator AddSeparator()
    {
        var separator = new Separator();

        separator.AddClass("vertical");

        return Add(separator);
    }
}
