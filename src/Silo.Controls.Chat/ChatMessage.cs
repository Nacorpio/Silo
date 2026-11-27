namespace Silo;

/// <summary>
/// One message of a chat: who wrote it, what it says, when, and how delivery is going.
/// </summary>
/// <remarks>
/// The same object is shown by a <see cref="MessageBubble"/> and whatever else the app builds on it. Change it and the views
/// follow, because it raises <see cref="Changed"/> for every change. A message is not thread safe: change it from the UI thread.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var message = new ChatMessage(new ChatAuthor("Nac", IsSelf: true), "Hello") { Status = MessageStatus.Sending };
///
/// message.Status = MessageStatus.Sent;
/// </code>
/// </example>
public sealed class ChatMessage
{
    /// <summary>The kind of an ordinary text message.</summary>
    public const string TextKind = "text";

    /// <summary>The kind of a notice that no one wrote, such as "Dana joined".</summary>
    public const string SystemKind = "system";

    /// <summary>The text.</summary>
    string text;

    /// <summary>The status.</summary>
    MessageStatus status = MessageStatus.Sent;

    /// <summary>The attachments.</summary>
    IReadOnlyList<ChatAttachment> attachments = [];

    /// <summary>Creates a message that was sent now.</summary>
    /// <param name="author">The author.</param>
    /// <param name="text">The text.</param>
    public ChatMessage(ChatAuthor author, string text)
    {
        ArgumentNullException.ThrowIfNull(author);

        Author = author;
        this.text = text ?? string.Empty;
        Id = Guid.NewGuid().ToString("N");
        Time = DateTimeOffset.Now;
    }

    /// <summary>Occurs when the text, the status or the attachments changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets or sets the id, which an app uses to find the message again. The default is a new unique id.</summary>
    public string Id { get; set; }

    /// <summary>Gets the author.</summary>
    public ChatAuthor Author { get; }

    /// <summary>Gets or sets when the message was sent.</summary>
    public DateTimeOffset Time { get; set; }

    /// <summary>
    /// Gets or sets the kind, which picks how the message is shown. A bubble handles <see cref="TextKind"/> and
    /// <see cref="SystemKind"/>; another kind is drawn by the template that <see cref="ChatOptions.Templates"/> holds for it.
    /// </summary>
    public string Kind { get; set; } = TextKind;

    /// <summary>Gets or sets the text.</summary>
    public string Text
    {
        get => text;
        set
        {
            value ??= string.Empty;

            if (text == value)
                return;

            text = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the delivery status. The default is <see cref="MessageStatus.Sent"/>.</summary>
    public MessageStatus Status
    {
        get => status;
        set
        {
            if (status == value)
                return;

            status = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the attachments.</summary>
    public IReadOnlyList<ChatAttachment> Attachments
    {
        get => attachments;
        set
        {
            attachments = value ?? [];

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
