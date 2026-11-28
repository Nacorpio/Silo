namespace Silo.Studio;

/// <summary>A <see cref="MessageList"/> for the review window that adds a message every couple of seconds, to try following the end.</summary>
internal sealed class StreamingMessageList : MessageList
{
    /// <summary>How long between two messages.</summary>
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>When the last message came.</summary>
    DateTimeOffset last = DateTimeOffset.UtcNow;

    /// <summary>The number of messages added so far.</summary>
    int added;

    /// <inheritdoc/>
    public override bool IsAnimating => true;

    /// <inheritdoc/>
    protected override bool OnFrame()
    {
        if (DateTimeOffset.UtcNow - last >= Interval)
        {
            last = DateTimeOffset.UtcNow;

            var author = added % 5 == 4 ? new ChatAuthor("Nac", IsSelf: true) : new ChatAuthor(added % 2 == 0 ? "Dana Reyes" : "Sam Okafor");

            Messages.Add(new ChatMessage(author, $"New message {++added}: " + (added % 3 == 0 ? "a longer one that has to wrap onto a second row when the window is narrow." : "short.")));
        }

        return true;
    }
}
