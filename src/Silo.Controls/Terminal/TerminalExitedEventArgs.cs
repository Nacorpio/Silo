namespace Silo;

/// <summary>Describes the end of an <see cref="ITerminalSession"/>.</summary>
/// <param name="exitCode">The exit code of the program, or 0 for a session that is not a program.</param>
public sealed class TerminalExitedEventArgs(int exitCode) : EventArgs
{
    /// <summary>Gets the exit code.</summary>
    public int ExitCode { get; } = exitCode;
}
