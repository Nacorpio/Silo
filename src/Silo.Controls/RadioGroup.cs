namespace Silo;

/// <summary>A container of <see cref="RadioButton"/>s of which exactly one can be on, with an index for the chosen one.</summary>
/// <remarks>
/// <para>
/// <see cref="Add"/> creates a RadioButton with the group's own name, so the buttons exclude one another without you naming a
/// group. Other Controls can be added to <c>Children</c> as well; they are not counted.
/// </para>
/// <para>The Default theme gives the RadioGroup a column layout. Apply styles again after the selection changes.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var size = new RadioGroup();
///
/// size.Add("Small");
/// size.Add("Large");
/// size.SelectedIndex = 0;
/// size.SelectionChanged += (_, _) => Resize(size.SelectedIndex);
/// </code>
/// </example>
public class RadioGroup : Control
{
    /// <summary>The group name that its RadioButtons share.</summary>
    readonly string group = Guid.NewGuid().ToString("N");

    /// <summary>The RadioButtons in order.</summary>
    readonly List<RadioButton> buttons = [];


    /// <summary>
    /// Runs before the first RadioGroup is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static RadioGroup()
    {
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets the number of RadioButtons that <see cref="Add"/> created.</summary>
    public int Count => buttons.Count;

    /// <summary>Gets or sets the index of the RadioButton that is on, or -1 for none. Values outside the group select nothing.</summary>
    public int SelectedIndex
    {
        get => buttons.FindIndex(button => button.IsChecked);
        set
        {
            if (value < 0 || value >= buttons.Count)
            {
                foreach (var button in buttons)
                    button.IsChecked = false;

                return;
            }

            buttons[value].IsChecked = true;
        }
    }

    /// <summary>Adds a RadioButton.</summary>
    /// <param name="text">The text of the RadioButton.</param>
    /// <returns>The new RadioButton, so that code can add a Class to it.</returns>
    public RadioButton Add(string text)
    {
        var button = new RadioButton { Text = text, Group = group };

        button.CheckedChanged += (_, _) =>
        {
            if (button.IsChecked)
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        };

        buttons.Add(button);
        Children.Add(button);

        return button;
    }
}
