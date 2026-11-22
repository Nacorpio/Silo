namespace Silo;

/// <summary>The title line of a <see cref="Dialog"/>.</summary>
/// <remarks>The Default theme makes it bold. Style it with the <c>DialogTitle</c> type.</remarks>
/// <example>
/// <code language="csharp">
/// var dialog = new Dialog { Title = "Delete file" };
///
/// dialog.TitleControl.AddClass("danger");
/// </code>
/// </example>
public sealed class DialogTitle : TextBlock
{
}
