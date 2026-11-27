namespace Silo;

/// <summary>
/// How much of a <see cref="ToolCall"/> a <see cref="ToolCallCard"/> shows.
/// </summary>
public enum ToolCallVerbosity
{
    /// <summary>The status icon and the name, and nothing to expand.</summary>
    Minimal,

    /// <summary>One row with the summary and the duration. It expands on a press, and a call that failed opens itself once.</summary>
    Normal,

    /// <summary>Always expanded, with the arguments and the output.</summary>
    Detailed,

    /// <summary>Detailed, plus the id, the times and the name of the status.</summary>
    Debug,
}
