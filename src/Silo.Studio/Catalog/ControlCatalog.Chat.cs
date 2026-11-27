using Silo.Platform.Sdl;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Chat category.</summary>
    static IEnumerable<CatalogEntry> Chat() =>
    [
        new("ChatInput", "Chat", CreateChatInput),
        new("MessageList", "Chat", () => CreateMessageList(40)),
        new("MessageBubble", "Chat", () => CreateBubble(false, "Sounds good to me. I will have the first version up this afternoon.")),
    ];

    /// <summary>Creates an empty chat input with a hint.</summary>
    /// <returns>The input.</returns>
    static ChatInput CreateChatInput() => new() { Placeholder = "Message #general" };

    /// <summary>Creates a chat input with several lines of text.</summary>
    /// <returns>The input.</returns>
    static ChatInput CreateWrittenChatInput()
    {
        var input = CreateChatInput();

        input.Text = "Here is the plan:\n1. Build the input\n2. Build the bubble\n3. Ship it";

        return input;
    }

    /// <summary>Creates a chat input with a long paragraph that wraps.</summary>
    /// <returns>The input.</returns>
    static ChatInput CreateLongChatInput()
    {
        var input = CreateChatInput();

        input.Text = "This message is long enough to wrap onto several rows, so the box has to grow with the wrapped rows and not with the lines you typed. Supercalifragilisticexpialidocious_is_one_word_wider_than_the_box_to_test_breaking_inside_a_word.\nA second line.";

        return input;
    }

    /// <summary>Creates a chat input that replies to a message.</summary>
    /// <returns>The input.</returns>
    static ChatInput CreateReplyChatInput()
    {
        var input = CreateChatInput();

        input.SetContext("Replying to Dana");
        input.Text = "Sounds good to me.";

        return input;
    }

    /// <summary>Gives a chat input a density.</summary>
    /// <param name="input">The input.</param>
    /// <param name="density">The density.</param>
    /// <returns>The same input.</returns>
    static ChatInput Dense(ChatInput input, ChatDensity density)
    {
        input.Density = density;

        return input;
    }

    /// <summary>Sets when a chat input shows its Send button.</summary>
    /// <param name="input">The input.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The same input.</returns>
    static ChatInput WithSend(ChatInput input, SendButtonMode mode)
    {
        input.SendButtonMode = mode;

        return input;
    }

    /// <summary>Creates a bubble with a message of Dana or of the user.</summary>
    /// <param name="own">Whether the message is the user's own.</param>
    /// <param name="text">The text.</param>
    /// <returns>The bubble.</returns>
    static MessageBubble CreateBubble(bool own, string text)
    {
        var author = own ? new ChatAuthor("Nac", IsSelf: true) : new ChatAuthor("Dana Reyes");

        return Resending(new MessageBubble { Message = new ChatMessage(author, text) { Time = new DateTimeOffset(2026, 11, 27, 14, 5, 0, TimeSpan.Zero) } });
    }

    /// <summary>Creates a bubble for the review variants.</summary>
    /// <param name="own">Whether the message is the user's own.</param>
    /// <param name="change">Changes the message or the options.</param>
    /// <returns>The bubble.</returns>
    static MessageBubble ChangedBubble(bool own, Action<MessageBubble> change)
    {
        var bubble = CreateBubble(own, own
            ? "Can you check the wrapped rows when the window is narrow? Supercalifragilisticexpialidocious_is_one_long_word."
            : "Here is the plan: the box grows with the wrapped rows, and the list comes next. I will push the draft tonight.");

        change(bubble);

        return bubble;
    }

    /// <summary>Creates a column of bubbles that share one set of options.</summary>
    /// <param name="change">Changes the shared options.</param>
    /// <returns>The conversation.</returns>
    static Box CreateConversation(Action<ChatOptions> change)
    {
        var options = new ChatOptions();
        var dana = new ChatAuthor("Dana Reyes");
        var me = new ChatAuthor("Nac", IsSelf: true);
        var sample = new (ChatAuthor Author, string Text, bool Grouped, MessageStatus Status)[]
        {
            (dana, "Morning! Did the review window open for you?", false, MessageStatus.Sent),
            (dana, "I changed the wrap so a long word breaks inside the word.", true, MessageStatus.Sent),
            (me, "Yes, it works. The box grows with the wrapped rows.", false, MessageStatus.Read),
            (me, "Sending the next one now.", true, MessageStatus.Sending),
            (me, "This one did not arrive.", true, MessageStatus.Failed),
        };

        change(options);

        var column = new Box();

        column.AddClass("conversation");

        foreach (var (author, text, grouped, status) in sample)
            column.Children.Add(Resending(new MessageBubble { Options = options, IsGrouped = grouped, Message = new ChatMessage(author, text) { Status = status, Time = new DateTimeOffset(2026, 11, 27, 14, 5, 0, TimeSpan.Zero) } }));

        return column;
    }

    /// <summary>Makes Retry in a sample succeed, so the gallery shows what a resend looks like.</summary>
    /// <param name="bubble">The bubble.</param>
    /// <returns>The same bubble.</returns>
    static MessageBubble Resending(MessageBubble bubble)
    {
        bubble.RetryRequested += (_, message) => message.Status = MessageStatus.Sent;

        return bubble;
    }

    /// <summary>The people of the sample chat.</summary>
    static readonly ChatAuthor[] People = [new("Dana Reyes"), new("Sam Okafor"), new("Nac", IsSelf: true)];

    /// <summary>The lines of the sample chat.</summary>
    static readonly string[] Lines =
    [
        "Morning! Did the review window open for you?",
        "Yes, it works. The box grows with the wrapped rows.",
        "Great. I changed the wrap so a long word breaks inside the word.",
        "Can you check the message list when the window is narrow?",
        "On it.",
        "The grouping looks right now: same author, same five minutes.",
        "One more thing: the jump button should show how many new messages came.",
        "Added. Scroll up and wait for the next message.",
    ];

    /// <summary>Creates a list with a conversation that runs over two days.</summary>
    /// <param name="count">The number of messages.</param>
    /// <returns>The list.</returns>
    static MessageList CreateMessageList(int count)
    {
        var list = new MessageList { Clipboard = new SdlClipboard() };
        var start = DateTimeOffset.Now.AddHours(-26);
        var time = start;

        for (var i = 0; i < count; i++)
        {
            // Runs of one author, with a pause now and then that ends a group, and a long one overnight.
            var author = People[(i / 3 + i % 2) % People.Length];

            time = time.AddMinutes(i % 7 == 6 ? 12 : i * 3 % 4 + 1).AddSeconds(i * 7 % 40);

            list.Messages.Add(new ChatMessage(author, Lines[i % Lines.Length]) { Time = time });
        }

        return list;
    }

    /// <summary>Creates a list that is scrolled to the start, with two messages that came later.</summary>
    /// <returns>The list.</returns>
    static MessageList CreateScrolledUpList()
    {
        var list = CreateMessageList(40);

        list.ScrollToStart();
        list.Messages.Add(new ChatMessage(People[0], "Are you still there?"));
        list.Messages.Add(new ChatMessage(People[0], "I pushed the fix."));

        return list;
    }

    /// <summary>Creates a list that loads older messages when the view is near the top.</summary>
    /// <returns>The list.</returns>
    static MessageList CreateLoadOlderList()
    {
        var list = CreateMessageList(14);
        var batch = 0;

        list.LoadOlderRequested += (_, _) =>
        {
            if (batch++ >= 3)
                return;

            var oldest = list.Messages[0].Time;

            for (var i = 0; i < 6; i++)
                list.Messages.Insert(0, new ChatMessage(People[(batch + i) % 2], $"Earlier message {batch}.{6 - i}") { Time = oldest.AddMinutes(-2 * (i + 1)) });
        };

        return list;
    }

    /// <summary>Creates a list that gets a new message every couple of seconds.</summary>
    /// <returns>The list.</returns>
    static MessageList CreateStreamingList()
    {
        var list = new StreamingMessageList { Clipboard = new SdlClipboard() };

        for (var i = 0; i < 12; i++)
            list.Messages.Add(new ChatMessage(People[i / 3 % 2], Lines[i % Lines.Length]) { Time = DateTimeOffset.Now.AddMinutes(-60 + i * 4) });

        return list;
    }

    /// <summary>Changes the options of a list.</summary>
    /// <param name="list">The list.</param>
    /// <param name="change">The change.</param>
    /// <returns>The same list.</returns>
    static MessageList WithOptions(MessageList list, Action<ChatOptions> change)
    {
        change(list.Options);

        return list;
    }
}
