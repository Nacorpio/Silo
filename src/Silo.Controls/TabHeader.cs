using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>The clickable title of one tab in a <see cref="TabView"/>, with an optional icon and buttons.</summary>
/// <remarks>
/// <para>
/// A TabHeader has the <c>selected</c> Pseudo-state while its tab is the one shown, so a Stylesheet can restyle it with
/// <c>TabHeader:selected</c>. The <see cref="TabView"/> creates the headers and sets the state.
/// </para>
/// <para>
/// The icon is scaled into a square of <c>icon-size</c> and placed by <c>icon-position</c> (<c>left</c>, <c>top</c> or
/// <c>right</c>) with <c>icon-gap</c> between it and the text. The buttons are <see cref="TabHeaderButton"/>s, placed by
/// <c>button-position</c> (<c>left</c> or <c>right</c>). Give the header <c>align-items: center</c> so they line up.
/// </para>
/// <para>
/// An icon that is a mask (<see cref="IImage.IsMask"/>) is drawn in the text color of the header, so it changes with
/// <c>TabHeader:selected</c> and the hover color like the text does.
/// </para>
/// <para>Arrange the tree again after changing the text, the icon or the buttons.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tabs = new TabView();
///
/// tabs.AddTab("General", general);
///
/// tabs.GetHeader(0).Icon = SkiaImage.Load("general.png");
/// tabs.GetHeader(0).AddButton(new TabHeaderButton { Text = "+" });
/// </code>
/// </example>
[PseudoStates("selected")]
public sealed class TabHeader : Button
{
    /// <summary>Which side of the text the icon goes on.</summary>
    public static readonly StyledProperty<IconPosition> IconPositionProperty =
        StyledProperty.Register<TabHeader, IconPosition>("icon-position", IconPosition.Left, ValueParsers.IconPlacement);

    /// <summary>The width and height of the icon in pixels.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        StyledProperty.Register<TabHeader, double>("icon-size", 16, ValueParsers.Pixels);

    /// <summary>The space between the icon and the text in pixels.</summary>
    public static readonly StyledProperty<double> IconGapProperty =
        StyledProperty.Register<TabHeader, double>("icon-gap", 6, ValueParsers.Pixels);

    /// <summary>Which side of the title the buttons go on.</summary>
    public static readonly StyledProperty<ButtonPosition> ButtonPositionProperty =
        StyledProperty.Register<TabHeader, ButtonPosition>("button-position", ButtonPosition.Right, ValueParsers.ButtonPlacement);


    /// <summary>Shows the icon and the title.</summary>
    readonly TabHeaderLabel label;

    /// <summary>The buttons, in the order they were added.</summary>
    readonly List<TabHeaderButton> buttons = [];

    /// <summary>Whether the tab is the one shown.</summary>
    bool isSelected;


    /// <summary>
    /// Runs before the first TabHeader is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TabHeader()
    {
    }

    /// <summary>Creates a TabHeader without icon and buttons.</summary>
    public TabHeader()
    {
        label = new TabHeaderLabel(this);

        Children.Add(label);
    }

    /// <summary>Gets or sets the title.</summary>
    public new string Text
    {
        get => label.Text;
        set => label.Text = value ?? string.Empty;
    }

    /// <summary>Gets or sets the icon, or <see langword="null"/> for none.</summary>
    public IImage? Icon
    {
        get => label.Icon;
        set => label.Icon = value;
    }

    /// <summary>Gets the buttons, in the order they were added.</summary>
    public IReadOnlyList<TabHeaderButton> Buttons => buttons;

    /// <summary>Gets or sets whether the tab is the one shown. Styles must be applied again after it changes.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            isSelected = value;

            SetPseudoState("selected", value);
        }
    }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Adds a button after the existing ones.</summary>
    /// <param name="button">The button.</param>
    public void AddButton(TabHeaderButton button)
    {
        ArgumentNullException.ThrowIfNull(button);

        buttons.Add(button);

        OrderChildren();
    }

    /// <summary>Removes a button.</summary>
    /// <param name="button">The button.</param>
    /// <returns><see langword="true"/> when the button was in the header.</returns>
    public bool RemoveButton(TabHeaderButton button)
    {
        if (!buttons.Remove(button))
            return false;

        Children.Remove(button);

        return true;
    }

    /// <inheritdoc/>
    protected override bool OnClick() => true;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(StyledProperty property)
    {
        base.OnPropertyChanged(property);

        if (property == ButtonPositionProperty)
            OrderChildren();
    }

    /// <summary>Puts the children in the order that <c>button-position</c> asks for.</summary>
    void OrderChildren()
    {
        foreach (var button in buttons)
            Children.Remove(button);

        Children.Remove(label);

        if (GetValue(ButtonPositionProperty) == ButtonPosition.Left)
            foreach (var button in buttons)
                Children.Add(button);

        Children.Add(label);

        if (GetValue(ButtonPositionProperty) == ButtonPosition.Right)
            foreach (var button in buttons)
                Children.Add(button);
    }
}
