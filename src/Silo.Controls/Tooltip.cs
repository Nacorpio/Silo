namespace Silo;

/// <summary>
/// The small box that appears when the pointer rests on a Control that has a <see cref="Control.Tooltip"/>: a line of text
/// and, when the Control has a <see cref="Control.TooltipShortcut"/>, the shortcut after it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TooltipPresenter"/> creates one when the pointer has rested long enough, so a Tooltip is rarely created by
/// hand. Style it with <c>Tooltip</c> rules, like any other Control. The text is a TextBlock, and the shortcut is a
/// TextBlock with the Class <c>shortcut</c>, so <c>Tooltip .shortcut</c> styles the shortcut alone. The Default theme sets
/// it in a monospace font, <c>--font-mono</c>, which starts with Fira Code.
/// </para>
/// <para>A Tooltip is shown as a passive overlay: the pointer goes through it, and it never takes a press.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// save.Tooltip = "Save the document";
/// save.TooltipShortcut = "Ctrl+S";
/// </code>
/// </example>
public class Tooltip : Control
{
    /// <summary>The line of text.</summary>
    readonly TextBlock label = new();

    /// <summary>The shortcut, which is a child only while there is one.</summary>
    readonly TextBlock keys = new();


    /// <summary>Creates an empty Tooltip.</summary>
    public Tooltip()
    {
        keys.AddClass("shortcut");
        Children.Add(label);
    }


    /// <summary>Gets or sets the line of text. The default is an empty string.</summary>
    /// <remarks>Arrange the tree again after changing it, since the size of the Tooltip depends on it.</remarks>
    public string Text
    {
        get => label.Text;
        set => label.Text = value;
    }

    /// <summary>Gets or sets the shortcut shown after the text, such as <c>Ctrl+S</c>. Empty shows none.</summary>
    /// <remarks>Apply the Stylesheets again after changing it, since a new part needs its styles.</remarks>
    public string Shortcut
    {
        get => keys.Text;
        set
        {
            keys.Text = value ?? string.Empty;

            Children.Remove(keys);

            if (keys.Text.Length > 0)
                Children.Add(keys);
        }
    }
}
