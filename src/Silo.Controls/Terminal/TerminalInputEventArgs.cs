namespace Silo;

/// <summary>Describes text that a <see cref="Terminal"/> sends to the program: typed keys, pasted text and answers to the program's questions.</summary>
/// <param name="text">The text to send, which can hold escape sequences.</param>
public sealed class TerminalInputEventArgs(string text) : EventArgs
{
    /// <summary>Gets the text to send to the program.</summary>
    public string Text { get; } = text;
}
