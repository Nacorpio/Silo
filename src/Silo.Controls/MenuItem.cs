namespace Silo;

/// <summary>One entry of a <see cref="ContextMenu"/>.</summary>
/// <param name="Text">The text of the entry.</param>
/// <param name="Click">What to do when the entry is picked, after the menu closed, or <see langword="null"/> for nothing.</param>
/// <example>
/// <code language="csharp">
/// var copy = new MenuItem("Copy", () => clipboard.Set(selection));
/// </code>
/// </example>
public sealed record MenuItem(string Text, Action? Click = null);
