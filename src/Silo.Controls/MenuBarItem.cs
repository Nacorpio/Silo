namespace Silo;

/// <summary>The title of one menu in a <see cref="MenuBar"/>.</summary>
/// <remarks>
/// <para>
/// A MenuBarItem has the <c>open</c> Pseudo-state while its menu is shown, so a Stylesheet can restyle it with
/// <c>MenuBarItem:open</c>. Style it with the <c>MenuBarItem</c> type.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var bar = new MenuBar();
/// var file = bar.AddMenu("File", [new MenuItem("Open", Open), new MenuItem("Exit", Exit)]);
///
/// file.AddClass("primary");
/// </code>
/// </example>
public sealed class MenuBarItem : Button
{
    /// <summary>Whether the menu is shown.</summary>
    bool isOpen;

    /// <summary>Creates a title with a menu.</summary>
    /// <param name="menu">The menu that opens below the title.</param>
    internal MenuBarItem(ContextMenu menu) => Menu = menu;

    /// <summary>Gets the menu that opens below the title.</summary>
    public ContextMenu Menu { get; }

    /// <summary>Gets the bar that the title is in, or <see langword="null"/> when it is not in one.</summary>
    public MenuBar? Bar => Parent as MenuBar;

    /// <summary>Gets whether the menu is shown. Styles must be applied again after it changes.</summary>
    public bool IsOpen
    {
        get => isOpen;
        internal set
        {
            isOpen = value;

            SetPseudoState("open", value);
        }
    }

    /// <inheritdoc/>
    protected override bool OnClick()
    {
        Bar?.Toggle(this);

        return true;
    }
}
