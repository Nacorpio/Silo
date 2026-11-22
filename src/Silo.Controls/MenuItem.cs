using Silo.Rendering;

namespace Silo;

/// <summary>One entry of a <see cref="ContextMenu"/>.</summary>
/// <param name="Text">The text of the entry.</param>
/// <param name="Click">What to do when the entry is picked, after the menu closed, or <see langword="null"/> for nothing.</param>
/// <param name="Icon">A small picture shown in front of the text, or <see langword="null"/> for none.</param>
/// <example>
/// <code language="csharp">
/// var copy = new MenuItem("Copy", () => clipboard.Set(selection), copyIcon);
/// </code>
/// </example>
public sealed record MenuItem(string Text, Action? Click = null, IImage? Icon = null);
