using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class MessageBubbleTests
{
    static readonly ChatAuthor Dana = new("Dana Reyes");
    static readonly ChatAuthor Me = new("Nac", IsSelf: true);

    static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in root.Children)
        {
            yield return child;

            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }

    static MessageBubble Bubble(ChatAuthor author, string text = "hello", ChatOptions? options = null)
        => new() { Options = options ?? new ChatOptions(), Message = new ChatMessage(author, text) };

    [Fact]
    public void The_text_goes_into_a_read_only_wrapped_area_and_follows_the_message()
    {
        var bubble = Bubble(Dana);

        Assert.True(bubble.Text.IsReadOnly);
        Assert.True(bubble.Text.WordWrap);
        Assert.Equal("hello", bubble.Text.Buffer.Text);

        bubble.Message!.Text = "edited";

        Assert.Equal("edited", bubble.Text.Buffer.Text);
    }

    [Fact]
    public void A_received_message_shows_the_avatar_and_the_author_and_a_grouped_one_hides_them()
    {
        var bubble = Bubble(Dana);

        Assert.Contains(bubble.Avatar, bubble.Children);
        Assert.Contains(Descendants(bubble), e => e is TextBlock t && t.HasClass("author"));

        bubble.IsGrouped = true;

        Assert.DoesNotContain(bubble.Avatar, bubble.Children);
        Assert.DoesNotContain(Descendants(bubble), e => e is TextBlock t && t.HasClass("author"));
        Assert.True(bubble.HasClass("grouped"));
    }

    [Fact]
    public void An_own_message_is_aligned_without_avatar_and_author_unless_the_options_say_otherwise()
    {
        var options = new ChatOptions();
        var bubble = Bubble(Me, options: options);

        Assert.True(bubble.IsAligned);
        Assert.True(bubble.HasClass("aligned"));
        Assert.DoesNotContain(bubble.Avatar, bubble.Children);

        options.AlignOwnMessages = false;

        Assert.False(bubble.IsAligned);
        Assert.False(bubble.HasClass("aligned"));
        Assert.True(bubble.HasClass("own"));
    }

    [Fact]
    public void The_options_give_the_style_and_density_classes_and_hide_avatars_and_times()
    {
        var options = new ChatOptions();
        var bubble = Bubble(Dana, options: options);

        Assert.True(bubble.HasClass("style-bubble"));

        options.BubbleStyle = BubbleStyle.Flat;
        options.Density = ChatDensity.Compact;

        Assert.True(bubble.HasClass("style-flat"));
        Assert.False(bubble.HasClass("style-bubble"));
        Assert.True(bubble.HasClass("density-compact"));

        options.ShowAvatars = false;
        options.ShowTimestamps = false;

        Assert.DoesNotContain(bubble.Avatar, bubble.Children);
        Assert.DoesNotContain(Descendants(bubble), e => e is TextBlock t && t.HasClass("time"));
    }

    [Fact]
    public void The_status_gives_a_class_and_a_state_and_a_failed_message_shows_Retry()
    {
        var bubble = Bubble(Me);

        bubble.Message!.Status = MessageStatus.Sending;

        Assert.True(bubble.HasClass("status-sending"));
        Assert.Contains("sending", bubble.PseudoStates);
        Assert.DoesNotContain(bubble.RetryButton, Descendants(bubble));

        bubble.Message.Status = MessageStatus.Failed;

        Assert.False(bubble.HasClass("status-sending"));
        Assert.Contains("failed", bubble.PseudoStates);
        Assert.Contains(bubble.RetryButton, Descendants(bubble));
    }

    [Fact]
    public void Retry_raises_RetryRequested_with_the_message()
    {
        var bubble = Bubble(Me);
        var raised = new List<ChatMessage>();

        bubble.RetryRequested += (_, message) => raised.Add(message);
        bubble.Message!.Status = MessageStatus.Failed;
        bubble.RetryButton.PointerDown();
        bubble.RetryButton.PointerUp(true);

        Assert.Equal([bubble.Message], raised);
    }

    [Fact]
    public void A_system_notice_has_no_avatar_author_or_status()
    {
        var message = new ChatMessage(Dana, "Dana joined") { Kind = ChatMessage.SystemKind };
        var bubble = new MessageBubble { Message = message };

        Assert.True(bubble.HasClass("system"));
        Assert.DoesNotContain(bubble.Avatar, bubble.Children);
        Assert.DoesNotContain(Descendants(bubble), e => e is TextBlock t && (t.HasClass("author") || t.HasClass("status")));
    }

    [Fact]
    public void A_template_draws_a_message_of_another_kind_in_place_of_the_text()
    {
        var options = new ChatOptions();
        var marker = new Box();

        options.Templates["image"] = _ => marker;

        var bubble = new MessageBubble { Options = options, Message = new ChatMessage(Dana, "pic") { Kind = "image" } };

        Assert.Contains(marker, Descendants(bubble));
        Assert.DoesNotContain(bubble.Text, Descendants(bubble));
    }

    [Fact]
    public void Attachment_names_are_listed()
    {
        var message = new ChatMessage(Dana, "see") { Attachments = [new ChatAttachment("a.png"), new ChatAttachment("b.md")] };
        var bubble = new MessageBubble { Message = message };

        Assert.Equal(["a.png", "b.md"], Descendants(bubble).OfType<TextBlock>().Where(t => t.HasClass("attachment")).Select(t => t.Text));
    }

    [Fact]
    public void A_change_of_the_options_is_raised_once_and_not_for_the_same_value()
    {
        var options = new ChatOptions();
        var raised = 0;

        options.Changed += (_, _) => raised++;
        options.Density = ChatDensity.Normal;
        options.Density = ChatDensity.Roomy;

        Assert.Equal(1, raised);
    }
}
