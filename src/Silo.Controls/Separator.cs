namespace Silo;

/// <summary>A thin line that divides the content of a panel, a menu or a toolbar.</summary>
/// <remarks>
/// <para>
/// A Separator has no content, so the theme gives it its look: a <c>background</c> and a thickness. The Default theme draws
/// a horizontal line one pixel high. The <c>vertical</c> Class turns it into a line that is one pixel wide and as high as
/// the row that holds it.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var toolbar = new Box();
/// var divider = new Separator();
///
/// divider.AddClass("vertical");
///
/// toolbar.Children.Add(new Button { Text = "Cut" });
/// toolbar.Children.Add(divider);
/// toolbar.Children.Add(new Button { Text = "Paste" });
/// </code>
/// </example>
public class Separator : Control
{
}
