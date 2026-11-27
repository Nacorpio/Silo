namespace Silo;

/// <summary>
/// The settings that every control of a chat shares: density, bubble style, alignment, and how each kind of message is drawn.
/// </summary>
/// <remarks>
/// Give one object to all the bubbles of a chat, bind it to the app's settings page, and a change shows everywhere because it
/// raises <see cref="Changed"/>. The review window edits it live in the same way.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var options = new ChatOptions { BubbleStyle = BubbleStyle.Flat, Density = ChatDensity.Compact };
///
/// options.Templates["image"] = message => new ImageMessage(message);
/// </code>
/// </example>
public sealed class ChatOptions
{
    /// <summary>The density.</summary>
    ChatDensity density = ChatDensity.Normal;

    /// <summary>The bubble style.</summary>
    BubbleStyle bubbleStyle = BubbleStyle.Bubble;

    /// <summary>Whether own messages sit on the right.</summary>
    bool alignOwnMessages = true;

    /// <summary>Whether the time is shown.</summary>
    bool showTimestamps = true;

    /// <summary>Whether the avatar is shown.</summary>
    bool showAvatars = true;

    /// <summary>Whether the hover toolbar is shown.</summary>
    bool showActions = true;

    /// <summary>Occurs when any setting changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets or sets how much room the bubbles leave around their content. The default is <see cref="ChatDensity.Normal"/>.</summary>
    public ChatDensity Density
    {
        get => density;
        set => Set(ref density, value);
    }

    /// <summary>Gets or sets how bubbles are drawn. The default is <see cref="BubbleStyle.Bubble"/>.</summary>
    public BubbleStyle BubbleStyle
    {
        get => bubbleStyle;
        set => Set(ref bubbleStyle, value);
    }

    /// <summary>Gets or sets whether messages of <see cref="ChatAuthor.IsSelf"/> sit on the right. The default is <see langword="true"/>.</summary>
    public bool AlignOwnMessages
    {
        get => alignOwnMessages;
        set => Set(ref alignOwnMessages, value);
    }

    /// <summary>Gets or sets whether the time of a message is shown. The default is <see langword="true"/>.</summary>
    public bool ShowTimestamps
    {
        get => showTimestamps;
        set => Set(ref showTimestamps, value);
    }

    /// <summary>Gets or sets whether the avatar is shown. The default is <see langword="true"/>.</summary>
    public bool ShowAvatars
    {
        get => showAvatars;
        set => Set(ref showAvatars, value);
    }

    /// <summary>Gets or sets whether a bubble shows Reply, Edit and Copy while the pointer is over it. The default is <see langword="true"/>.</summary>
    public bool ShowActions
    {
        get => showActions;
        set => Set(ref showActions, value);
    }

    /// <summary>
    /// Gets the templates that draw the messages of a kind that a bubble does not handle itself, by <see cref="ChatMessage.Kind"/>.
    /// A bubble puts the control that a template makes in place of its text.
    /// </summary>
    public Dictionary<string, Func<ChatMessage, Control>> Templates { get; } = [];

    /// <summary>Stores a new value and raises <see cref="Changed"/> when it differs.</summary>
    /// <typeparam name="T">The type of the setting.</typeparam>
    /// <param name="field">The field that holds the setting.</param>
    /// <param name="value">The new value.</param>
    void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
