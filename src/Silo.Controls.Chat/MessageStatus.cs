namespace Silo;

/// <summary>
/// Where a <see cref="ChatMessage"/> is in its delivery. The built-in statuses are static members; an app adds its own by making
/// another one.
/// </summary>
/// <param name="Name">The kebab-case name. A bubble gets the class <c>status-</c> plus this name, so a theme can style one status.</param>
/// <param name="Label">The text that people read, such as <c>"Sent"</c>.</param>
/// <param name="Kind">What the status means for how the message is shown.</param>
/// <example>
/// <code language="csharp">
/// var queued = new MessageStatus("queued", "Queued", MessageStatusKind.Active);
///
/// message.Status = queued;
/// </code>
/// </example>
public readonly record struct MessageStatus(string Name, string Label, MessageStatusKind Kind)
{
    /// <summary>The message is being sent.</summary>
    public static MessageStatus Sending { get; } = new("sending", "Sending…", MessageStatusKind.Active);

    /// <summary>The message arrived.</summary>
    public static MessageStatus Sent { get; } = new("sent", "Sent", MessageStatusKind.Neutral);

    /// <summary>The other side read the message.</summary>
    public static MessageStatus Read { get; } = new("read", "Read", MessageStatusKind.Neutral);

    /// <summary>The message did not arrive.</summary>
    public static MessageStatus Failed { get; } = new("failed", "Failed to send", MessageStatusKind.Failure);
}
