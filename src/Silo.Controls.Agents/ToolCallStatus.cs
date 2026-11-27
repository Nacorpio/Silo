namespace Silo;

/// <summary>
/// Where a <see cref="ToolCall"/> is in its life. The built-in statuses are static members; an app adds its own by making another one.
/// </summary>
/// <param name="Name">The kebab-case name. A card gets the class <c>status-</c> plus this name, so a theme can style one status.</param>
/// <param name="Label">The text that people read, such as <c>"Waiting for approval"</c>.</param>
/// <param name="Kind">What the status means for how the call is shown.</param>
/// <example>
/// <code language="csharp">
/// var limited = new ToolCallStatus("rate-limited", "Rate limited", ToolCallStatusKind.Waiting);
///
/// call.Status = limited;
/// </code>
/// </example>
public readonly record struct ToolCallStatus(string Name, string Label, ToolCallStatusKind Kind)
{
    /// <summary>The call is queued and has not started.</summary>
    public static ToolCallStatus Pending { get; } = new("pending", "Pending", ToolCallStatusKind.Neutral);

    /// <summary>The call waits for the user to allow it.</summary>
    public static ToolCallStatus AwaitingApproval { get; } = new("awaiting-approval", "Waiting for approval", ToolCallStatusKind.Waiting);

    /// <summary>The call is running.</summary>
    public static ToolCallStatus Running { get; } = new("running", "Running", ToolCallStatusKind.Active);

    /// <summary>The call finished well.</summary>
    public static ToolCallStatus Succeeded { get; } = new("succeeded", "Succeeded", ToolCallStatusKind.Success);

    /// <summary>The call failed.</summary>
    public static ToolCallStatus Failed { get; } = new("failed", "Failed", ToolCallStatusKind.Failure);

    /// <summary>The call was stopped before it finished.</summary>
    public static ToolCallStatus Cancelled { get; } = new("cancelled", "Cancelled", ToolCallStatusKind.Neutral);
}
