using System.Text;

namespace Silo;

/// <summary>What a command of a <see cref="CommandSession"/> can see and do while it runs: its arguments, and the terminal to write to.</summary>
/// <example>
/// <code language="csharp">
/// session.Add("greet", "Says hello", context =>
/// {
///     context.WriteLine($"Hello, {(context.Arguments.Count > 0 ? context.Arguments[0] : "world")}!");
/// });
/// </code>
/// </example>
public sealed class CommandContext
{
    readonly CommandSession session;

    internal CommandContext(CommandSession session, string name, string line, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        this.session = session;

        Name = name;
        Line = line;
        Arguments = arguments;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the session that runs the command.</summary>
    public CommandSession Session => session;

    /// <summary>Gets the name of the command, as it was typed.</summary>
    public string Name { get; }

    /// <summary>Gets the whole line that was entered.</summary>
    public string Line { get; }

    /// <summary>Gets the arguments after the name. Text in single or double quotes is one argument.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Gets a token that is cancelled when the user presses Ctrl+C while the command runs.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets the number of columns of the terminal.</summary>
    public int Columns => session.Columns;

    /// <summary>Gets the number of rows of the terminal.</summary>
    public int Rows => session.Rows;

    /// <summary>Writes text. Line feeds start a new line at the left edge.</summary>
    /// <param name="text">The text, which can hold escape sequences.</param>
    public void Write(string text) => session.Emit(text);

    /// <summary>Writes text and ends the line.</summary>
    /// <param name="text">The text.</param>
    public void WriteLine(string text = "") => session.Emit(text + "\n");

    /// <summary>Writes a line in red.</summary>
    /// <param name="text">The text.</param>
    public void WriteError(string text) => session.Emit("\u001b[31m" + text + "\u001b[0m\n");

    /// <summary>Clears the screen and moves the cursor to the top left.</summary>
    public void Clear() => session.Emit("\u001b[H\u001b[2J");
}
