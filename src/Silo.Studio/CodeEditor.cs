namespace Silo.Studio;

/// <summary>
/// The editor of Studio's text view of a theme: a <see cref="TextArea"/> that shows its line numbers.
/// </summary>
/// <example>
/// <code language="csharp">
/// var editor = new CodeEditor(new TextBuffer(File.ReadAllText(path))) { Name = "source" };
/// </code>
/// </example>
public sealed class CodeEditor : TextArea
{
    /// <summary>Creates an editor.</summary>
    /// <param name="buffer">The buffer to show, or <see langword="null"/> for an empty one.</param>
    public CodeEditor(TextBuffer? buffer = null)
        : base(buffer)
        => ShowLineNumbers = true;
}
