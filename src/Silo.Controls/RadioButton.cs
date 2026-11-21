namespace Silo;

/// <summary>
/// A Button that is on when it is the chosen one of a group. Clicking it turns it on and turns off the others of its group.
/// </summary>
/// <remarks>
/// A group is the RadioButtons with the same <see cref="Group"/> among the children of one parent. Clicking the chosen
/// RadioButton again keeps it on.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var small = new RadioButton { Text = "Small", Group = "size", IsChecked = true };
/// var large = new RadioButton { Text = "Large", Group = "size" };
///
/// panel.Children.Add(small);
/// panel.Children.Add(large);
/// </code>
/// </example>
public class RadioButton : ToggleButton
{
    /// <summary>Gets or sets the name of the group. RadioButtons with the same name next to each other exclude one another.</summary>
    public string Group { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override double IndicatorRadius => 8;

    /// <inheritdoc/>
    protected override bool ValueAfterClick() => true;

    /// <inheritdoc/>
    protected override void OnCheckedChanged()
    {
        if (!IsChecked || Parent is null)
            return;

        foreach (var sibling in Parent.Children)
            if (sibling is RadioButton other && other != this && other.Group == Group)
                other.IsChecked = false;
    }
}
