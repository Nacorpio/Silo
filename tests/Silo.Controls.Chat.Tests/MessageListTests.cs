using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class MessageListTests
{
    sealed class FakeClipboard : IClipboard
    {
        public string Text { get; set; } = string.Empty;

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    static readonly ChatAuthor Dana = new("Dana Reyes");
    static readonly ChatAuthor Sam = new("Sam Okafor");
    static readonly ChatAuthor Me = new("Nac", IsSelf: true);
    static readonly DateTimeOffset Noon = new DateTimeOffset(DateTime.Today.AddHours(12), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.AddHours(12)));

    static StyleResolver Styles = null!;

    static (Box Root, MessageList List) Setup(int count = 0)
    {
        var list = new MessageList();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var local = JsoncStylesheetReader.Read("""{ "rules": { "Box": { "align-items": "start" }, "MessageList": { "width": "320px", "height": "200px" } } }""", "t.jsonc");

        root.Children.Add(list);
        Styles = new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), ChatTheme.Load(), local]);

        root.ApplyStyles(Styles);

        for (var i = 0; i < count; i++)
            list.Messages.Add(new ChatMessage(i % 2 == 0 ? Dana : Sam, $"message {i}") { Time = Noon.AddMinutes(i * 10) });

        Frame(root);

        return (root, list);
    }

    static void Frame(Box root)
    {
        for (var i = 0; i < 2; i++)
        {
            // Bubbles that were made since the last frame get their styles, as the window does.
            root.ApplyStyles(Styles);
            root.Arrange(new Size(400, 300));
            root.Render(new DisplayListBuilder());
        }
    }

    [Fact]
    public void The_view_starts_at_the_end_and_follows_new_messages()
    {
        var (root, list) = Setup(30);

        Assert.True(list.Viewer.MaxOffset > 0);
        Assert.Equal(list.Viewer.MaxOffset, list.Viewer.Offset);
        Assert.True(list.IsFollowing);

        list.Messages.Add(new ChatMessage(Dana, "one more"));
        Frame(root);

        Assert.Equal(list.Viewer.MaxOffset, list.Viewer.Offset);
    }

    [Fact]
    public void Scrolled_up_the_view_stays_and_the_unread_count_and_the_jump_button_show()
    {
        var (root, list) = Setup(30);

        list.Viewer.ScrollBy(-150);
        Frame(root);

        var offset = list.Viewer.Offset;

        Assert.False(list.IsFollowing);

        list.Messages.Add(new ChatMessage(Dana, "new 1"));
        list.Messages.Add(new ChatMessage(Sam, "new 2"));
        Frame(root);

        Assert.Equal(offset, list.Viewer.Offset);
        Assert.Equal(2, list.UnreadCount);
        Assert.Equal("↓ 2 new messages", list.JumpButton.Text);
        Assert.True(list.JumpButton.Bounds.Width > 0);

        list.JumpButton.PointerDown();
        list.JumpButton.PointerUp(true);
        Frame(root);

        Assert.True(list.IsFollowing);
        Assert.Equal(0, list.UnreadCount);
        Assert.Equal(list.Viewer.MaxOffset, list.Viewer.Offset);
    }

    [Fact]
    public void A_message_of_your_own_scrolls_to_the_end_even_when_scrolled_up()
    {
        var (root, list) = Setup(30);

        list.Viewer.ScrollBy(-150);
        Frame(root);
        list.Messages.Add(new ChatMessage(Me, "mine"));
        Frame(root);

        Assert.True(list.IsFollowing);
        Assert.Equal(list.Viewer.MaxOffset, list.Viewer.Offset);
    }

    [Fact]
    public void Messages_inserted_above_keep_what_is_on_screen_where_it_is()
    {
        var (root, list) = Setup(30);

        list.Viewer.ScrollBy(-150);
        Frame(root);

        var fromBottom = list.Viewer.MaxOffset - list.Viewer.Offset;

        for (var i = 0; i < 5; i++)
            list.Messages.Insert(0, new ChatMessage(Dana, $"older {i}") { Time = Noon.AddHours(-1).AddMinutes(-i * 10) });

        Frame(root);

        Assert.Equal(fromBottom, list.Viewer.MaxOffset - list.Viewer.Offset, precision: 3);
        Assert.Equal(0, list.UnreadCount);
    }

    [Fact]
    public void Older_messages_are_asked_for_once_near_the_top_for_each_oldest_message()
    {
        var (root, list) = Setup(30);
        var asked = 0;

        list.LoadOlderRequested += (_, _) => asked++;
        list.Viewer.ScrollBy(-10000);
        Frame(root);
        Frame(root);

        Assert.Equal(1, asked);

        list.Messages.Insert(0, new ChatMessage(Dana, "older") { Time = Noon.AddHours(-2) });
        list.Viewer.ScrollBy(-10000);
        Frame(root);

        Assert.Equal(2, asked);
    }

    [Fact]
    public void A_message_from_the_same_author_within_the_window_on_the_same_day_is_grouped()
    {
        var (_, list) = Setup();

        var first = new ChatMessage(Dana, "a") { Time = Noon };
        var close = new ChatMessage(Dana, "b") { Time = Noon.AddMinutes(4) };
        var late = new ChatMessage(Dana, "c") { Time = Noon.AddMinutes(11) };
        var other = new ChatMessage(Sam, "d") { Time = Noon.AddMinutes(12) };

        foreach (var message in new[] { first, close, late, other })
            list.Messages.Add(message);

        Assert.False(list.BubbleOf(first)!.IsGrouped);
        Assert.True(list.BubbleOf(close)!.IsGrouped);
        Assert.False(list.BubbleOf(late)!.IsGrouped);
        Assert.False(list.BubbleOf(other)!.IsGrouped);

        list.GroupingWindow = TimeSpan.FromMinutes(30);

        Assert.True(list.BubbleOf(late)!.IsGrouped);
    }

    [Fact]
    public void A_row_starts_each_day_and_names_today_and_yesterday()
    {
        var (_, list) = Setup();
        var clock = new FixedTime(Noon.AddHours(1));

        list.TimeProvider = clock;
        list.Messages.Add(new ChatMessage(Dana, "old") { Time = Noon.AddDays(-3) });
        list.Messages.Add(new ChatMessage(Dana, "yesterday") { Time = Noon.AddDays(-1) });
        list.Messages.Add(new ChatMessage(Dana, "today") { Time = Noon });
        list.Messages.Add(new ChatMessage(Dana, "also today") { Time = Noon.AddMinutes(1) });

        var labels = Descendants(list).OfType<TextBlock>().Where(t => t.HasClass("date-label")).Select(t => t.Text).ToList();

        Assert.Equal(3, labels.Count);
        Assert.Equal("Yesterday", labels[1]);
        Assert.Equal("Today", labels[2]);
        Assert.False(list.BubbleOf(list.Messages[1])!.IsGrouped);
    }

    [Fact]
    public void Removing_a_message_removes_its_bubble_and_the_collection_can_be_cleared()
    {
        var (_, list) = Setup(3);
        var gone = list.Messages[1];

        list.Messages.RemoveAt(1);

        Assert.Null(list.BubbleOf(gone));
        Assert.Equal(2, Descendants(list).OfType<MessageBubble>().Count());

        list.Messages.Clear();

        Assert.Empty(Descendants(list).OfType<MessageBubble>());
        Assert.True(list.IsFollowing);
    }

    [Fact]
    public void The_toolbar_shows_while_the_pointer_is_over_a_bubble_and_Edit_is_only_for_own_messages()
    {
        var (root, list) = Setup();
        var clipboard = new FakeClipboard();
        var replied = new List<ChatMessage>();
        var theirs = new ChatMessage(Dana, "hello there");
        var mine = new ChatMessage(Me, "my words");

        list.Clipboard = clipboard;
        list.ReplyRequested += (_, m) => replied.Add(m);
        list.Messages.Add(theirs);
        list.Messages.Add(mine);
        Frame(root);

        var bubble = list.BubbleOf(theirs)!;
        var own = list.BubbleOf(mine)!;

        Assert.Equal(0, bubble.Actions.Bounds.Width);

        bubble.SetHovered(true);
        Frame(root);

        Assert.True(bubble.Actions.Bounds.Width > 0);
        Assert.DoesNotContain(Descendants(bubble.Actions).OfType<Button>(), b => b.HasClass("edit"));

        own.SetHovered(true);
        Frame(root);

        Assert.Contains(Descendants(own.Actions).OfType<Button>(), b => b.HasClass("edit"));

        var copy = Descendants(bubble.Actions).OfType<Button>().Single(b => b.HasClass("copy"));

        copy.PointerDown();
        copy.PointerUp(true);

        Assert.Equal("hello there", clipboard.Text);

        var reply = Descendants(bubble.Actions).OfType<Button>().Single(b => b.HasClass("reply"));

        reply.PointerDown();
        reply.PointerUp(true);

        Assert.Equal([theirs], replied);

        bubble.SetHovered(false);
        list.Options.ShowActions = false;
        Frame(root);

        Assert.Equal(0, bubble.Actions.Bounds.Width);
    }

    [Fact]
    public void Options_are_shared_by_every_bubble()
    {
        var (_, list) = Setup(3);

        list.Options.BubbleStyle = BubbleStyle.Flat;

        Assert.All(Descendants(list).OfType<MessageBubble>(), b => Assert.True(b.HasClass("style-flat")));

        var options = new ChatOptions { BubbleStyle = BubbleStyle.Lines };

        list.Options = options;

        Assert.All(Descendants(list).OfType<MessageBubble>(), b => Assert.True(b.HasClass("style-lines")));
    }

    sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in root.Children)
        {
            yield return child;

            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }
}
