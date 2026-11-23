namespace Silo;

/// <summary>
/// A program, or something that acts like one, that a <see cref="Terminal"/> talks to: it produces output bytes and receives the
/// keys that the user types.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Terminal.Attach"/> connects a session to a terminal. It subscribes to <see cref="Output"/> and <see cref="Exited"/>, which a
/// session may raise from any thread, sends typed text to <see cref="Send"/>, tells the session about size changes with
/// <see cref="Resize"/>, and starts it with <see cref="Start"/>. Two sessions come with Silo: <see cref="ProcessSession"/> runs a
/// program and <see cref="CommandSession"/> runs C# command handlers.
/// </para>
/// </remarks>
public interface ITerminalSession : IDisposable
{
    /// <summary>Raised, possibly from another thread, when the session has bytes of UTF-8 text with escape sequences for the terminal.</summary>
    event EventHandler<TerminalOutputEventArgs>? Output;

    /// <summary>Raised, possibly from another thread, when the session has ended.</summary>
    event EventHandler<TerminalExitedEventArgs>? Exited;

    /// <summary>Starts the session. Output only comes after this call.</summary>
    /// <param name="columns">The number of columns of the terminal.</param>
    /// <param name="rows">The number of rows of the terminal.</param>
    void Start(int columns, int rows);

    /// <summary>Gives the session what the user typed: text, and escape sequences for the special keys.</summary>
    /// <param name="text">The text.</param>
    void Send(string text);

    /// <summary>Tells the session that the terminal has a new size.</summary>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    void Resize(int columns, int rows);
}
