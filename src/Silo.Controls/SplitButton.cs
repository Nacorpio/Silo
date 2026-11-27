namespace Silo;

/// <summary>
/// A Button with an arrow next to it: pressing the button does its main action, pressing the arrow opens a menu of related actions.
/// </summary>
/// <remarks>
/// <para>
/// Use it when one action is by far the most common, such as Save, and the others, such as Save As, should be one press away. Set
/// <see cref="Text"/> and handle <see cref="Click"/> for the main action, and give the menu with <see cref="Entries"/> or
/// <see cref="Menu"/>. The menu opens below the whole control, aligned with the main button.
/// </para>
/// <para>
/// It is a row of two Buttons: <see cref="Main"/> with the Class <c>split-main</c> and <see cref="Arrow"/>, a
/// <see cref="DropDownButton"/> with the Class <c>split-arrow</c>. The Default theme puts a small gap between them, so style them
/// like any other Button.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var save = new SplitButton { Text = "Save", Entries = [new MenuItem("Save &amp;As…", SaveAs)] };
///
/// save.Click += (_, _) => Save();
/// </code>
/// </example>
[PseudoStates("hover", "active")]
public class SplitButton : Control
{
    /// <summary>The button with the main action.</summary>
    readonly Button main = new();

    /// <summary>The button that opens the menu.</summary>
    readonly DropDownButton arrow = new();

    /// <summary>Creates a SplitButton with an empty menu.</summary>
    public SplitButton()
    {
        main.AddClass("split-main");
        arrow.AddClass("split-arrow");

        arrow.Anchor = this;
        main.Click += (_, e) => Click?.Invoke(this, e);

        Children.Add(main);
        Children.Add(arrow);
    }

    /// <summary>Raised when the main button is pressed. Pressing the arrow does not raise it.</summary>
    public event EventHandler? Click;

    /// <summary>Gets or sets the text of the main button.</summary>
    public string Text
    {
        get => main.Text;
        set => main.Text = value;
    }

    /// <summary>Gets the button with the main action.</summary>
    public Button Main => main;

    /// <summary>Gets the button with the arrow that opens the menu.</summary>
    public DropDownButton Arrow => arrow;

    /// <summary>Gets or sets the menu that the arrow opens.</summary>
    public ContextMenu Menu
    {
        get => arrow.Menu;
        set => arrow.Menu = value;
    }

    /// <summary>Gets or sets the entries of <see cref="Menu"/>.</summary>
    public IReadOnlyList<MenuItem> Entries
    {
        get => arrow.Entries;
        set => arrow.Entries = value;
    }
}
