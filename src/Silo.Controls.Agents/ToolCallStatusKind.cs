namespace Silo;

/// <summary>
/// What a <see cref="ToolCallStatus"/> means for how a call is shown, so that a status an app adds needs no code in the controls.
/// </summary>
public enum ToolCallStatusKind
{
    /// <summary>Nothing is happening and nothing went wrong, such as pending or cancelled.</summary>
    Neutral,

    /// <summary>The call waits for something, such as an approval. It has the <c>:waiting</c> state.</summary>
    Waiting,

    /// <summary>The call is running. It shows a spinner, its duration ticks, and it has the <c>:running</c> state.</summary>
    Active,

    /// <summary>The call finished well. It has the <c>:succeeded</c> state.</summary>
    Success,

    /// <summary>The call failed. A card opens itself once, and it has the <c>:failed</c> state.</summary>
    Failure,
}
