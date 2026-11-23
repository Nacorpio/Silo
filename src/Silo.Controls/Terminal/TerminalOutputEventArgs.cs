namespace Silo;

/// <summary>Describes output of an <see cref="ITerminalSession"/>.</summary>
/// <param name="data">The bytes of UTF-8 text, with escape sequences. A character can be split between two events.</param>
public sealed class TerminalOutputEventArgs(byte[] data) : EventArgs
{
    /// <summary>Gets the bytes.</summary>
    public byte[] Data { get; } = data;

    /// <summary>Creates the event data for a text.</summary>
    /// <param name="text">The text.</param>
    public static TerminalOutputEventArgs FromText(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));
}
