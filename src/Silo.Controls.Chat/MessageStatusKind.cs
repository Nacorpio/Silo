namespace Silo;

/// <summary>
/// What a <see cref="MessageStatus"/> means for how a message is shown, so that a status an app adds needs no code in the controls.
/// </summary>
public enum MessageStatusKind
{
    /// <summary>Nothing special, such as sent or read.</summary>
    Neutral,

    /// <summary>The message is on its way. It has the <c>:sending</c> state.</summary>
    Active,

    /// <summary>The message failed to arrive. It shows a Retry button and has the <c>:failed</c> state.</summary>
    Failure,
}
