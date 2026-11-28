using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Silo.Layout;

namespace Silo;

/// <summary>
/// A scrolling list of <see cref="MessageBubble"/>s that follows the end, groups messages from the same author and separates days.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Messages"/> is an <see cref="ObservableCollection{T}"/>: add, insert and remove messages and the list follows. While the
/// view is at the bottom, a new message scrolls into view. Scrolled up, the view stays where it is and a button shows how many new
/// messages came; messages that are inserted above, such as older ones that were loaded, keep what is on screen where it is. A message
/// of the user's own (<see cref="ChatAuthor.IsSelf"/>) always scrolls to the end. Near the top the list raises
/// <see cref="LoadOlderRequested"/> once for the oldest message it has.
/// </para>
/// <para>
/// A text message follows one from the same author on the same day within <see cref="GroupingWindow"/> as a group: its bubble gets
/// <see cref="MessageBubble.IsGrouped"/>. A row with "Today", "Yesterday" or the date starts each day. Every bubble shares
/// <see cref="Options"/>. The list builds one bubble per message and does not virtualize yet.
/// </para>
/// <para>
/// The theme sees the Classes <c>viewer</c> (the scroller), <c>messages</c> (the column), <c>date-separator</c>, <c>date-label</c> and
/// <c>jump</c> (the button, which needs a <c>width</c> and a <c>height</c> there).
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var list = new MessageList();
///
/// list.Messages.Add(new ChatMessage(new ChatAuthor("Dana"), "Morning!"));
/// list.ReplyRequested += (_, message) => composer.SetContext($"Replying to {message.Author.Name}");
/// list.LoadOlderRequested += (_, _) => history.LoadBefore(list.Messages[0]);
/// </code>
/// </example>
public class MessageList : Control
{
    /// <summary>How close to the top, in pixels, the view must be before older messages are asked for.</summary>
    const double LoadOlderDistance = 80;

    /// <summary>How close to the bottom, in pixels, still counts as at the end.</summary>
    const double EndTolerance = 2;

    /// <summary>The bubbles, by message, so that a bubble keeps its text selection while the list changes.</summary>
    readonly Dictionary<ChatMessage, MessageBubble> bubbles = [];

    /// <summary>The scroller.</summary>
    readonly ScrollViewer viewer = new();

    /// <summary>The column of bubbles and date rows.</summary>
    readonly Box column = new();

    /// <summary>The wrapper that shows or hides the jump button.</summary>
    readonly ClippedBox jumpHost = new();

    /// <summary>The button that scrolls to the latest message.</summary>
    readonly Button jump = new();

    /// <summary>The messages.</summary>
    readonly ObservableCollection<ChatMessage> messages = [];

    /// <summary>The settings that every bubble shares.</summary>
    ChatOptions options = new();

    /// <summary>The clipboard.</summary>
    IClipboard? clipboard;

    /// <summary>The longest time between two messages of a group.</summary>
    TimeSpan groupingWindow = TimeSpan.FromMinutes(5);

    /// <summary>The time source for "Today" and "Yesterday".</summary>
    TimeProvider time = TimeProvider.System;

    /// <summary>Whether the view is at the end and follows it.</summary>
    bool following = true;

    /// <summary>Whether the next frame scrolls to the end.</summary>
    bool scrollToEnd = true;

    /// <summary>Whether the next frame scrolls to the start.</summary>
    bool scrollToStart;

    /// <summary>The distance from the bottom to restore after messages were inserted above, or <see langword="null"/>.</summary>
    double? restoreFromBottom;

    /// <summary>The number of messages that came while the view was scrolled up.</summary>
    int unread;

    /// <summary>The oldest message that older ones were last asked for.</summary>
    ChatMessage? askedFor;

    static MessageList()
    {
    }

    /// <summary>Creates an empty list.</summary>
    public MessageList()
    {
        viewer.AddClass("viewer");
        column.AddClass("messages");
        jumpHost.AddClass("jump-host");
        jump.AddClass("jump");

        viewer.Content = column;
        jumpHost.Children.Add(jump);

        jump.Click += (_, _) => ScrollToEnd();
        messages.CollectionChanged += OnMessagesChanged;

        Children.Add(viewer);
        Children.Add(jumpHost);

        ShowJump();
    }

    /// <summary>Occurs when the view is near the top, once for each oldest message. Insert the older messages at the start of <see cref="Messages"/> and the view keeps what is on screen where it is.</summary>
    public event EventHandler? LoadOlderRequested;

    /// <summary>Occurs when Reply on a bubble is pressed. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? ReplyRequested;

    /// <summary>Occurs when Edit on a bubble is pressed. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? EditRequested;

    /// <summary>Occurs after Copy on a bubble put the text on the clipboard. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? CopyRequested;

    /// <summary>Occurs when Retry on a failed bubble is pressed. The argument is the message.</summary>
    public event EventHandler<ChatMessage>? RetryRequested;

    /// <summary>Gets the messages, oldest first.</summary>
    public ObservableCollection<ChatMessage> Messages => messages;

    /// <summary>Gets or sets the settings that every bubble shares. A new list has settings of its own.</summary>
    public ChatOptions Options
    {
        get => options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            options = value;

            foreach (var bubble in bubbles.Values)
                bubble.Options = value;
        }
    }

    /// <summary>Gets or sets the clipboard that Copy and Ctrl+C write to.</summary>
    public IClipboard? Clipboard
    {
        get => clipboard;
        set
        {
            clipboard = value;

            foreach (var bubble in bubbles.Values)
                bubble.Clipboard = value;
        }
    }

    /// <summary>Gets or sets the longest time between two messages of a group. The default is five minutes.</summary>
    public TimeSpan GroupingWindow
    {
        get => groupingWindow;
        set
        {
            groupingWindow = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the time source that decides what "Today" is. The default is the system clock.</summary>
    public TimeProvider TimeProvider
    {
        get => time;
        set
        {
            time = value ?? TimeProvider.System;

            Rebuild();
        }
    }

    /// <summary>Gets whether the view is at the end, so that new messages scroll into view.</summary>
    public bool IsFollowing => following;

    /// <summary>Gets how many messages came while the view was scrolled up.</summary>
    public int UnreadCount => unread;

    /// <summary>Gets the scroller.</summary>
    public ScrollViewer Viewer => viewer;

    /// <summary>Gets the button that scrolls to the latest message.</summary>
    public Button JumpButton => jump;

    /// <summary>Scrolls to the oldest message and stops following the end, so that messages that come later count as unread.</summary>
    public void ScrollToStart()
    {
        following = false;
        scrollToEnd = false;
        scrollToStart = true;

        ShowJump();
    }

    /// <summary>Scrolls to the latest message and follows the end again.</summary>
    public void ScrollToEnd()
    {
        following = true;
        scrollToEnd = true;
        unread = 0;

        ShowJump();
    }

    /// <summary>Gets the bubble of a message, or <see langword="null"/> when the message is not in the list.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The bubble.</returns>
    public MessageBubble? BubbleOf(ChatMessage message) => bubbles.GetValueOrDefault(message);

    /// <inheritdoc/>
    protected override bool IsInFlow(Control child) => child != jumpHost;

    /// <inheritdoc/>
    protected override bool HasOutOfFlowChildren => true;

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect content)
    {
        PlaceJump();

        return false;
    }

    /// <inheritdoc/>
    protected override void OnRender(Silo.Rendering.IDrawingContext context, Rect bounds)
    {
        Follow();
        PlaceJump();

        base.OnRender(context, bounds);
    }

    /// <summary>Applies what the last change asked for, once the layout knows the new extent, and notes whether the user scrolled.</summary>
    void Follow()
    {
        if (scrollToEnd)
        {
            viewer.ScrollBy(viewer.MaxOffset - viewer.Offset);

            scrollToEnd = false;
        }
        else if (scrollToStart)
        {
            viewer.ScrollToTop();

            scrollToStart = false;
        }
        else if (restoreFromBottom is { } fromBottom)
        {
            viewer.ScrollBy(viewer.MaxOffset - fromBottom - viewer.Offset);

            restoreFromBottom = null;
        }
        else
        {
            var atEnd = viewer.Offset >= viewer.MaxOffset - EndTolerance;

            if (atEnd != following)
            {
                following = atEnd;

                if (atEnd)
                    unread = 0;

                ShowJump();
            }
        }

        if (messages.Count > 0 && viewer.Offset < LoadOlderDistance && askedFor != messages[0])
        {
            askedFor = messages[0];

            LoadOlderRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Puts the jump button at the bottom centre of the view when it applies, and gives it no size otherwise.</summary>
    void PlaceJump()
    {
        var width = jumpHost.GetValue(WidthProperty)?.Value ?? 0;
        var height = jumpHost.GetValue(HeightProperty)?.Value ?? 0;

        if (following || width <= 0 || height <= 0)
        {
            PlaceChild(jumpHost, new Rect(Bounds.Width, Bounds.Height, 0, 0));

            return;
        }

        PlaceChild(jumpHost, new Rect((Bounds.Width - width) / 2, Math.Max(0, Bounds.Height - height - 12), width, height));
    }

    /// <summary>Writes the text of the jump button.</summary>
    void ShowJump() => jump.Text = unread > 0
        ? $"↓ {unread.ToString(CultureInfo.InvariantCulture)} new {(unread == 1 ? "message" : "messages")}"
        : "↓ Jump to latest";

    /// <summary>Reacts to a change of the messages.</summary>
    /// <param name="sender">The collection.</param>
    /// <param name="e">What changed.</param>
    void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var added = e.Action == NotifyCollectionChangedAction.Add ? e.NewItems!.Cast<ChatMessage>().ToList() : [];
        var appended = added.Count > 0 && e.NewStartingIndex + added.Count >= messages.Count;

        // Messages inserted above what is on screen move it down, so the distance from the bottom is what stays the same.
        if (!following && !appended && restoreFromBottom is null && e.Action != NotifyCollectionChangedAction.Reset)
            restoreFromBottom = viewer.MaxOffset - viewer.Offset;

        Rebuild();

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            following = true;
            scrollToEnd = true;
            unread = 0;
            askedFor = null;
            restoreFromBottom = null;
        }
        else if (following || added.Any(m => m.Author.IsSelf))
        {
            following = true;
            scrollToEnd = true;
            unread = 0;
        }
        else if (appended)
        {
            unread += added.Count;
        }

        ShowJump();
    }

    /// <summary>Builds the column again from the messages, keeping the bubbles that are still there.</summary>
    void Rebuild()
    {
        column.Children.Clear();

        var present = messages.ToHashSet();

        foreach (var gone in bubbles.Keys.Where(m => !present.Contains(m)).ToList())
        {
            bubbles[gone].Message = null;
            bubbles.Remove(gone);
        }

        ChatMessage? previous = null;

        foreach (var message in messages)
        {
            if (previous is null || !SameDay(previous, message))
                column.Children.Add(Separator(message.Time));

            if (!bubbles.TryGetValue(message, out var bubble))
            {
                bubble = Create(message);
                bubbles[message] = bubble;
            }

            bubble.IsGrouped = previous is not null && Groups(previous, message);

            column.Children.Add(bubble);

            previous = message;
        }
    }

    /// <summary>Makes the bubble of a message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The bubble.</returns>
    MessageBubble Create(ChatMessage message)
    {
        var bubble = new MessageBubble { Options = options, Clipboard = clipboard, Message = message };

        bubble.ReplyRequested += (_, m) => ReplyRequested?.Invoke(this, m);
        bubble.EditRequested += (_, m) => EditRequested?.Invoke(this, m);
        bubble.CopyRequested += (_, m) => CopyRequested?.Invoke(this, m);
        bubble.RetryRequested += (_, m) => RetryRequested?.Invoke(this, m);

        return bubble;
    }

    /// <summary>Tells whether a message continues the group of the one before it.</summary>
    /// <param name="previous">The earlier message.</param>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when it has the same author, is a text message and is close enough in time.</returns>
    bool Groups(ChatMessage previous, ChatMessage message)
        => previous.Author == message.Author
            && previous.Kind == ChatMessage.TextKind
            && message.Kind == ChatMessage.TextKind
            && SameDay(previous, message)
            && message.Time - previous.Time <= groupingWindow;

    /// <summary>Tells whether two messages were sent on the same day, on this computer's clock.</summary>
    /// <param name="a">One message.</param>
    /// <param name="b">The other.</param>
    /// <returns><see langword="true"/> for the same day.</returns>
    static bool SameDay(ChatMessage a, ChatMessage b) => a.Time.ToLocalTime().Date == b.Time.ToLocalTime().Date;

    /// <summary>Makes the row that starts a day.</summary>
    /// <param name="day">A time on that day.</param>
    /// <returns>The row.</returns>
    Box Separator(DateTimeOffset day)
    {
        var row = new Box();
        var label = new TextBlock { Text = DayLabel(day) };

        row.AddClass("date-separator");
        label.AddClass("date-label");
        row.Children.Add(label);

        return row;
    }

    /// <summary>Names a day.</summary>
    /// <param name="day">A time on that day.</param>
    /// <returns><c>Today</c>, <c>Yesterday</c>, or the date.</returns>
    string DayLabel(DateTimeOffset day)
    {
        var date = day.ToLocalTime().Date;
        var today = time.GetLocalNow().Date;

        if (date == today)
            return "Today";

        if (date == today.AddDays(-1))
            return "Yesterday";

        return date.ToString(date.Year == today.Year ? "dddd d MMMM" : "d MMMM yyyy", CultureInfo.CurrentCulture);
    }
}
