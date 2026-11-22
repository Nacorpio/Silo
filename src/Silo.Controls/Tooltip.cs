namespace Silo;

/// <summary>
/// The small box of text that appears when the pointer rests on a Control that has a <see cref="Control.Tooltip"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TooltipPresenter"/> creates one when the pointer has rested long enough, so a Tooltip is rarely created by
/// hand. Style it with <c>Tooltip</c> rules, like any other text Control.
/// </para>
/// <para>A Tooltip is shown as a passive overlay: the pointer goes through it, and it never takes a press.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// save.Tooltip = "Save the document (Ctrl+S)";
/// </code>
/// </example>
public class Tooltip : TextControl
{
}
