namespace Silo;

/// <summary>The row of <see cref="TabHeader"/>s above the content of a <see cref="TabView"/>.</summary>
/// <remarks>
/// <para>The Default theme lays the headers out in a row. Style it with the <c>TabStrip</c> type.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tabs = new TabView();
///
/// tabs.Strip.AddClass("compact");
/// </code>
/// </example>
public sealed class TabStrip : Control
{
}
