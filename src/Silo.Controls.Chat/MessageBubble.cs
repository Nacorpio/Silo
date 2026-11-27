using Silo.Layout;

namespace Silo;

/// <summary>
/// One <see cref="ChatMessage"/>: avatar, author, time, text, attachments and delivery status.
/// </summary>
/// <remarks>
/// <para>
/// The text is a read-only <see cref="TextArea"/> that wraps, so it can be selected and copied. A message of another kind than
/// text or system is drawn by the template that <see cref="ChatOptions.Templates"/> holds for it, in place of the text.
/// </para>
/// <para>
/// <see cref="Options"/> gives the density, the <see cref="BubbleStyle"/>, the alignment of own messages and whether avatars and
/// times show. Share one <see cref="ChatOptions"/> between all bubbles. A list sets <see cref="IsGrouped"/> on a message that
/// follows one from the same author, which hides its avatar and name.
/// </para>
/// <para>
/// Delivery shows for own messages: the status label, and a Retry button on a failed one that raises <see cref="RetryRequested"/>.
/// The bubble never resends a message itself.
/// </para>
/// <para>
/// The theme sees the Classes <c>own</c>, <c>grouped</c>, <c>system</c>, <c>style-bubble</c>, <c>style-flat</c>, <c>style-lines</c>,
/// <c>aligned</c>, <c>density-compact</c>, <c>density-roomy</c> and <c>status-</c> plus the status name, and the states
/// <c>:sending</c> and <c>:failed</c>. The parts have the Classes <c>avatar</c>, <c>content</c>, <c>header</c>, <c>author</c>,
/// <c>time</c>, <c>text</c>, <c>attachments</c>, <c>attachment</c>, <c>footer</c>, <c>status</c> and <c>retry</c>. While the pointer
/// is over the bubble and <see cref="ChatOptions.ShowActions"/> is on, a small toolbar with the Class <c>actions</c> shows Reply,
/// Edit (own messages only) and Copy, as Buttons with the Classes <c>reply</c>, <c>edit</c> and <c>copy</c>. The toolbar needs a
/// <c>width</c> and a <c>height</c> in the theme.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var options = new ChatOptions();
/// var bubble = new MessageBubble { Options = options, Message = new ChatMessage(new ChatAuthor("Dana"), "Sounds good") };
///
/// bubble.RetryRequested += (_, message) => chat.Resend(message);
/// </code>
/// </example>
[PseudoStates("sending", "failed")]
public class MessageBubble : Control
{
    /// <summary>The format of the time of a message.</summary>
    const string TimeFormat = "HH:mm";

    /// <summary>The picture or initials of the author.</summary>
    readonly Avatar avatar = new();

    /// <summary>The column right of the avatar.</summary>
    readonly Box content = new();

    /// <summary>The row with the author and the time.</summary>
    readonly Box header = new();

    /// <summary>The name of the author.</summary>
    readonly TextBlock author = new();

    /// <summary>The time.</summary>
    readonly TextBlock time = new();

    /// <summary>The text.</summary>
    readonly TextArea text = new() { IsReadOnly = true, WordWrap = true, MinimumLines = 1, MaximumLines = int.MaxValue };

    /// <summary>The row with the attachment names.</summary>
    readonly Box attachments = new();

    /// <summary>The row with the status and the Retry button.</summary>
    readonly Box footer = new();

    /// <summary>The status label.</summary>
    readonly TextBlock status = new();

    /// <summary>Asks to send a failed message again.</summary>
    readonly Button retry = new() { Text = "Retry" };

    /// <summary>The toolbar that shows while the pointer is over the bubble.</summary>
    readonly ClippedBox actions = new();

    /// <summary>Starts a reply to the message.</summary>
    readonly Button reply = new() { Text = "Reply" };

    /// <summary>Starts an edit of the message.</summary>
    readonly Button edit = new() { Text = "Edit" };

    /// <summary>Copies the text of the message.</summary>
    readonly Button copy = new() { Text = "Copy" };

    /// <summary>The settings.</summary>
    ChatOptions options = new();

    /// <summary>The message.</summary>
    ChatMessage? message;

    /// <summary>The control a template made for the message, if any.</summary>
    Control? custom;

    /// <summary>Whether the message follows one from the same author.</summary>
    bool grouped;

    /// <summary>The class that the status gave the bubble.</summary>
    string statusClass = string.Empty;

    /// <summary>The Classes that the options gave the bubble.</summary>
    readonly List<string> optionClasses = [];


    static MessageBubble()
    {
    }

    /// <summary>Creates a bubble with no message.</summary>
    public MessageBubble()
    {
        avatar.AddClass("avatar");
        content.AddClass("content");
        header.AddClass("header");
        author.AddClass("author");
        time.AddClass("time");
        text.AddClass("text");
        attachments.AddClass("attachments");
        footer.AddClass("footer");
        status.AddClass("status");
        retry.AddClass("retry");
        actions.AddClass("actions");
        reply.AddClass("reply");
        edit.AddClass("edit");
        copy.AddClass("copy");

        reply.Click += (_, _) => Raise(ReplyRequested);
        edit.Click += (_, _) => Raise(EditRequested);
        copy.Click += (_, _) =>
        {
            Clipboard?.SetText(message?.Text ?? string.Empty);

            Raise(CopyRequested);
        };

        retry.Click += (_, _) =>
        {
            if (message is not null)
                RetryRequested?.Invoke(this, message);
        };

        options.Changed += OnOptionsChanged;

        Rebuild();
    }


    /// <summary>Occurs when the Retry button of a failed message is pressed. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? RetryRequested;

    /// <summary>Occurs when Reply on the toolbar is pressed. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? ReplyRequested;

    /// <summary>Occurs when Edit on the toolbar is pressed. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? EditRequested;

    /// <summary>Occurs after Copy on the toolbar put the text on the clipboard. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? CopyRequested;


    /// <summary>Gets or sets the message that the bubble shows.</summary>
    public ChatMessage? Message
    {
        get => message;
        set
        {
            if (message == value)
                return;

            if (message is not null)
                message.Changed -= OnMessageChanged;

            message = value;

            if (message is not null)
                message.Changed += OnMessageChanged;

            custom = null;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the settings, which can be shared with other chat controls. A new bubble has settings of its own.</summary>
    public ChatOptions Options
    {
        get => options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (options == value)
                return;

            options.Changed -= OnOptionsChanged;

            options = value;
            options.Changed += OnOptionsChanged;

            custom = null;

            Rebuild();
        }
    }

    /// <summary>Gets or sets whether the message follows one from the same author, which hides the avatar and the name.</summary>
    public bool IsGrouped
    {
        get => grouped;
        set
        {
            if (grouped == value)
                return;

            grouped = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the clipboard that Copy and Ctrl+C in the text write to.</summary>
    public IClipboard? Clipboard
    {
        get => text.Clipboard;
        set => text.Clipboard = value;
    }

    /// <summary>Gets the toolbar that shows while the pointer is over the bubble.</summary>
    public Control Actions => actions;

    /// <summary>Gets the part that shows the text, which can be selected.</summary>
    public TextArea Text => text;

    /// <summary>Gets the Retry button.</summary>
    public Button RetryButton => retry;

    /// <summary>Gets the avatar.</summary>
    public Avatar Avatar => avatar;

    /// <summary>Gets whether the message is the user's own and sits on the right. Never in the Lines style, which is a log.</summary>
    public bool IsAligned => message?.Author.IsSelf == true && options.AlignOwnMessages && options.BubbleStyle != BubbleStyle.Lines && message.Kind != ChatMessage.SystemKind;

    /// <inheritdoc/>
    protected override bool IsInFlow(Control child) => child != actions;

    /// <inheritdoc/>
    protected override bool HasOutOfFlowChildren => true;

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect content)
    {
        PlaceActions();

        return false;
    }

    /// <inheritdoc/>
    protected override void OnRender(Silo.Rendering.IDrawingContext context, Rect bounds)
    {
        // Whether the pointer is over the bubble is known now, which the layout pass before it did not know.
        PlaceActions();

        base.OnRender(context, bounds);
    }

    /// <summary>Puts the toolbar at the top right of the content when the pointer is over the bubble, and gives it no size otherwise.</summary>
    void PlaceActions()
    {
        if (!Children.Contains(actions))
            return;

        var width = actions.GetValue(WidthProperty)?.Value ?? 0;
        var height = actions.GetValue(HeightProperty)?.Value ?? 0;
        var shown = PseudoStates.Contains("hover") && options.ShowActions && width > 0 && height > 0;

        if (!shown)
        {
            PlaceChild(actions, new Rect(Bounds.Width, 0, 0, 0));

            return;
        }

        var box = content.Bounds;

        PlaceChild(actions, new Rect(Math.Max(0, box.Right - width - 6), box.Y - height + 4, width, height));
    }

    /// <summary>Raises an event of the toolbar with the message.</summary>
    /// <param name="handler">The event.</param>
    void Raise(EventHandler<ChatMessage>? handler)
    {
        if (message is not null)
            handler?.Invoke(this, message);
    }

    /// <summary>Reacts to a change of the message.</summary>
    /// <param name="sender">The message.</param>
    /// <param name="e">Nothing.</param>
    void OnMessageChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>Reacts to a change of the settings.</summary>
    /// <param name="sender">The settings.</param>
    /// <param name="e">Nothing.</param>
    void OnOptionsChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>Writes the message into the parts, gives the bubble its Classes and puts the parts that apply in the tree.</summary>
    void Rebuild()
    {
        ApplyClasses();

        Children.Clear();
        content.Children.Clear();
        header.Children.Clear();
        footer.Children.Clear();
        attachments.Children.Clear();

        if (message is null)
            return;

        var system = message.Kind == ChatMessage.SystemKind;
        var lines = options.BubbleStyle == BubbleStyle.Lines;
        var own = IsAligned;
        var stamp = message.Time.ToLocalTime().ToString(TimeFormat, System.Globalization.CultureInfo.InvariantCulture);

        avatar.DisplayName = message.Author.Name;
        avatar.Source = message.Author.Avatar;
        author.Text = message.Author.Name;
        time.Text = stamp;
        status.Text = message.Status.Label;

        SetText(message.Text);

        if (!system && !own && !grouped && !lines && options.ShowAvatars)
            Children.Add(avatar);
        else if (!system && !own && !lines && options.ShowAvatars)
            Children.Add(Spacer());

        if (system)
        {
            content.Children.Add(text);
            Children.Add(content);

            return;
        }

        if (lines)
        {
            if (options.ShowTimestamps)
                header.Children.Add(time);

            header.Children.Add(author);
            content.Children.Add(header);
        }
        else if (!grouped && !own)
        {
            header.Children.Add(author);

            if (options.ShowTimestamps)
                header.Children.Add(time);

            content.Children.Add(header);
        }

        content.Children.Add(Body());

        foreach (var file in message.Attachments)
        {
            var chip = new TextBlock { Text = file.Name };

            chip.AddClass("attachment");
            attachments.Children.Add(chip);
        }

        if (attachments.Children.Count > 0)
            content.Children.Add(attachments);

        if (lines && message.Status.Kind == MessageStatusKind.Failure)
            content.Children.Add(retry);

        if (own)
        {
            if (options.ShowTimestamps)
                footer.Children.Add(time);

            footer.Children.Add(status);

            if (message.Status.Kind == MessageStatusKind.Failure)
                footer.Children.Add(retry);

            content.Children.Add(footer);
        }

        Children.Add(content);

        AddActions();
    }

    /// <summary>Fills the toolbar with the buttons that apply and puts it in the tree.</summary>
    void AddActions()
    {
        actions.Children.Clear();
        actions.Children.Add(reply);

        if (message?.Author.IsSelf == true)
            actions.Children.Add(edit);

        actions.Children.Add(copy);

        Children.Add(actions);
    }

    /// <summary>Gets the control that shows the body: the one a template makes, else the text.</summary>
    /// <returns>The control.</returns>
    Control Body()
    {
        if (message is not null && message.Kind != ChatMessage.TextKind && options.Templates.TryGetValue(message.Kind, out var template))
            return custom ??= template(message);

        return text;
    }

    /// <summary>Makes an empty box as wide as the avatar, so a grouped message lines up with the one above.</summary>
    /// <returns>The box.</returns>
    static Box Spacer()
    {
        var box = new Box();

        box.AddClass("avatar-space");

        return box;
    }

    /// <summary>Puts text in the read-only area.</summary>
    /// <param name="value">The text.</param>
    void SetText(string value)
    {
        if (text.Buffer.Text == value)
            return;

        text.Buffer.Reset(value);
    }

    /// <summary>Gives the bubble the Classes and states of the message and the options.</summary>
    void ApplyClasses()
    {
        foreach (var name in optionClasses)
            RemoveClass(name);

        optionClasses.Clear();

        if (statusClass.Length > 0)
            RemoveClass(statusClass);

        statusClass = string.Empty;

        SetPseudoState("sending", false);
        SetPseudoState("failed", false);

        optionClasses.Add($"style-{options.BubbleStyle.ToString().ToLowerInvariant()}");

        if (options.Density != ChatDensity.Normal)
            optionClasses.Add($"density-{options.Density.ToString().ToLowerInvariant()}");

        if (grouped)
            optionClasses.Add("grouped");

        if (message?.Kind == ChatMessage.SystemKind)
            optionClasses.Add("system");

        if (message?.Author.IsSelf == true)
            optionClasses.Add("own");

        if (IsAligned)
            optionClasses.Add("aligned");

        if (message is not null)
        {
            statusClass = $"status-{message.Status.Name}";

            AddClass(statusClass);

            SetPseudoState("sending", message.Status.Kind == MessageStatusKind.Active);
            SetPseudoState("failed", message.Status.Kind == MessageStatusKind.Failure);
        }

        foreach (var name in optionClasses)
            AddClass(name);
    }
}
