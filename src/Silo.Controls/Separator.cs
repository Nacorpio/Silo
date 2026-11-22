namespace Silo;

/// <summary>
/// A thin line that divides groups of Controls, such as the items of a menu or the sections of a panel.
/// </summary>
/// <remarks>
/// A Separator has no content, so it draws only what its style gives it: the default theme makes it 1px high with the
/// border color as background. Give it a <c>width</c> and a <c>height</c> to make a vertical one.
/// </remarks>
/// <example>
/// <code language="csharp">
/// panel.Children.Add(new TextBlock { Text = "General" });
/// panel.Children.Add(new Separator());
/// panel.Children.Add(new TextBlock { Text = "Advanced" });
/// </code>
/// </example>
public class Separator : Control
{
}
