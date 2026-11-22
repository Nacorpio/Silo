namespace Silo;

/// <summary>A small button inside a <see cref="TabHeader"/>, such as the one that closes the tab.</summary>
/// <remarks>
/// <para>
/// Pressing a TabHeaderButton does not select the tab. Style it with the <c>TabHeaderButton</c> type, since the
/// <c>Button</c> and <c>TabHeader</c> rules do not apply to it.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var pin = new TabHeaderButton { Text = "P" };
///
/// pin.Click += (_, _) => Pin(tabs.SelectedIndex);
/// tabs.GetHeader(0).AddButton(pin);
/// </code>
/// </example>
public sealed class TabHeaderButton : Button
{
    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    protected override bool OnClick() => true;
}
