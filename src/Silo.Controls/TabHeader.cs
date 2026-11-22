namespace Silo;

/// <summary>The clickable title of one tab in a <see cref="TabView"/>.</summary>
/// <remarks>
/// <para>
/// A TabHeader has the <c>selected</c> Pseudo-state while its tab is the one shown, so a Stylesheet can restyle it with
/// <c>TabHeader:selected</c>. The <see cref="TabView"/> creates the headers and sets the state.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tabs = new TabView();
///
/// tabs.AddTab("General", general);
/// tabs.GetHeader(0).AddClass("first");
/// </code>
/// </example>
public sealed class TabHeader : Button
{
    /// <summary>Whether the tab is the one shown.</summary>
    bool isSelected;

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

    /// <inheritdoc/>
    protected override bool OnClick() => true;
}
