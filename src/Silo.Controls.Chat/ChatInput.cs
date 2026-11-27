namespace Silo;

/// <summary>
/// The box that a message is typed in: a text area that grows with its text, a Send button, an optional bar that says what the
/// message replies to or edits, and a slot above the text for attachments.
/// </summary>
/// <remarks>
/// <para>
/// Enter sends the message and Shift+Enter breaks the line. With <see cref="SendOnEnter"/> off, Ctrl+Enter sends and Enter breaks
/// the line. <see cref="SendButtonMode"/> decides when the Send button is shown. A message that is only white space is not sent. Sending raises <see cref="Submitted"/> with the trimmed text and
/// clears the box. Escape closes the context bar when there is one.
/// </para>
/// <para>
/// The area is <see cref="MinimumLines"/> lines tall and grows to <see cref="MaximumLines"/>, then scrolls. It does not wrap
/// long lines yet.
/// </para>
/// <para>
/// The theme sees <see cref="Density"/> as the Classes <c>density-compact</c> and <c>density-roomy</c> on the input. The parts
/// have the Classes <c>context</c>, <c>attachments</c>, <c>row</c>, <c>composer</c> and <c>send</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var input = new ChatInput { Placeholder = "Message #general" };
///
/// input.Submitted += (_, text) => Console.WriteLine(text);
/// input.SetContext("Replying to Dana");
/// </code>
/// </example>
public class ChatInput : Control
{
    /// <summary>The text area.</summary>
    readonly ComposerArea composer = new() { MinimumLines = 1, MaximumLines = 8, WordWrap = true };

    /// <summary>The Send button.</summary>
    readonly Button send = new() { Text = "Send" };

    /// <summary>The bar with the context text and its close button.</summary>
    readonly Box contextBar = new();

    /// <summary>The text of the context bar.</summary>
    readonly TextBlock contextText = new();

    /// <summary>The slot for the attachments.</summary>
    readonly Box attachmentSlot = new();

    /// <summary>The row with the text area and the Send button.</summary>
    readonly Box row = new();

    /// <summary>The context that is shown, or <see langword="null"/>.</summary>
    string? context;

    /// <summary>The control in the attachment slot.</summary>
    Control? attachments;

    /// <summary>The density.</summary>
    ChatDensity density = ChatDensity.Normal;

    /// <summary>When the Send button is shown.</summary>
    SendButtonMode sendButtonMode = SendButtonMode.WhenText;

    /// <summary>Creates an empty input.</summary>
    public ChatInput()
    {
        var close = new Button { Text = "×" };

        close.AddClass("close");
        contextBar.AddClass("context");
        contextText.AddClass("context-text");
        attachmentSlot.AddClass("attachments");
        row.AddClass("row");
        composer.AddClass("composer");
        send.AddClass("send");

        close.Click += (_, _) => ClearContext();
        send.Click += (_, _) => Send();

        composer.Intercept = Intercept;
        composer.TextChanged += (_, _) =>
        {
            if (sendButtonMode == SendButtonMode.WhenText)
                Rebuild();

            TextChanged?.Invoke(this, EventArgs.Empty);
        };

        contextBar.Children.Add(contextText);
        contextBar.Children.Add(close);

        row.Children.Add(composer);
        row.Children.Add(send);

        Rebuild();
    }

    /// <summary>Raised when a message is sent, with its text trimmed.</summary>
    public event EventHandler<string>? Submitted;

    /// <summary>Raised after the text changed.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Raised after the context bar was closed, by its button or by Escape.</summary>
    public event EventHandler? ContextCleared;

    /// <summary>Gets or sets the text. Setting it replaces what was typed.</summary>
    public string Text
    {
        get => composer.Buffer.Text;
        set
        {
            composer.Buffer.SelectAll();

            if (string.IsNullOrEmpty(value))
                composer.Buffer.Delete();
            else
                composer.Buffer.Insert(value);
        }
    }

    /// <summary>Gets or sets the hint that is shown while the box is empty.</summary>
    public string Placeholder
    {
        get => composer.Placeholder;
        set => composer.Placeholder = value ?? string.Empty;
    }

    /// <summary>Gets or sets whether Enter sends. When it is off, Ctrl+Enter sends and Enter breaks the line. The default is <see langword="true"/>.</summary>
    public bool SendOnEnter { get; set; } = true;

    /// <summary>Gets or sets the height of the text area when it is empty, in lines. The default is 1.</summary>
    public int MinimumLines
    {
        get => composer.MinimumLines;
        set => composer.MinimumLines = value;
    }

    /// <summary>Gets or sets the height that the text area grows to, in lines, before it scrolls. The default is 8.</summary>
    public int MaximumLines
    {
        get => composer.MaximumLines ?? composer.MinimumLines;
        set => composer.MaximumLines = value;
    }

    /// <summary>Gets or sets how much room the input leaves. The default is <see cref="ChatDensity.Normal"/>.</summary>
    public ChatDensity Density
    {
        get => density;
        set
        {
            density = value;

            _ = RemoveClass("density-compact");
            _ = RemoveClass("density-roomy");

            _ = value switch
            {
                ChatDensity.Compact => AddClass("density-compact"),
                ChatDensity.Roomy => AddClass("density-roomy"),
                _ => false,
            };
        }
    }

    /// <summary>Gets or sets when the Send button is shown. The default is <see cref="SendButtonMode.WhenText"/>.</summary>
    public SendButtonMode SendButtonMode
    {
        get => sendButtonMode;
        set
        {
            sendButtonMode = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the text of the Send button, such as <c>Post</c> or an icon glyph. The default is <c>Send</c>.</summary>
    public string SendText
    {
        get => send.Text;
        set => send.Text = value ?? string.Empty;
    }

    /// <summary>Gets or sets the control that is shown above the text, such as a tray of attachments, or <see langword="null"/> for none.</summary>
    public Control? Attachments
    {
        get => attachments;
        set
        {
            attachments = value;

            attachmentSlot.Children.Clear();

            if (value is not null)
                attachmentSlot.Children.Add(value);

            Rebuild();
        }
    }

    /// <summary>Gets the text of the context bar, or <see langword="null"/> when it is closed.</summary>
    public string? Context => context;

    /// <summary>Gets or sets what the Copy, Cut and Paste keys use. Without it those keys do nothing.</summary>
    public IClipboard? Clipboard
    {
        get => composer.Clipboard;
        set => composer.Clipboard = value;
    }

    /// <summary>Gets the text area, which takes the focus.</summary>
    public TextArea Input => composer;

    /// <summary>Gets the Send button.</summary>
    public Button SendButton => send;

    /// <summary>Gets whether a message can be sent: there is text that is not only white space.</summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(Text);

    /// <summary>Shows the bar that says what the message replies to or edits.</summary>
    /// <param name="text">The text, such as <c>Replying to Dana</c>.</param>
    public void SetContext(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        context = text;
        contextText.Text = text;

        Rebuild();
    }

    /// <summary>Closes the context bar.</summary>
    /// <returns><see langword="true"/> when there was one.</returns>
    public bool ClearContext()
    {
        if (context is null)
            return false;

        context = null;

        Rebuild();

        ContextCleared?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Sends the message: raises <see cref="Submitted"/> with the trimmed text, then clears the box.</summary>
    /// <returns><see langword="true"/> when a message was sent, <see langword="false"/> when there was no text.</returns>
    public bool Send()
    {
        if (!CanSend)
            return false;

        var message = Text.Trim();

        Text = string.Empty;

        Submitted?.Invoke(this, message);

        return true;
    }

    /// <summary>Decides what a key does before the text area sees it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys.</param>
    /// <returns><see langword="true"/> when the key was used up.</returns>
    bool Intercept(Key key, KeyModifiers modifiers)
    {
        if (key == Key.Escape)
            return ClearContext();

        if (key != Key.Enter || modifiers.HasFlag(KeyModifiers.Shift))
            return false;

        if (modifiers.HasFlag(KeyModifiers.Control) != !SendOnEnter)
            return false;

        Send();

        return true;
    }

    /// <summary>Puts the parts that are shown in the tree.</summary>
    void Rebuild()
    {
        Children.Clear();

        if (context is not null)
            Children.Add(contextBar);

        if (attachments is not null)
            Children.Add(attachmentSlot);

        row.Children.Clear();
        row.Children.Add(composer);

        if (sendButtonMode == SendButtonMode.Always || (sendButtonMode == SendButtonMode.WhenText && CanSend))
            row.Children.Add(send);

        Children.Add(row);
    }
}
