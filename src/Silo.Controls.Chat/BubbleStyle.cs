namespace Silo;

/// <summary>
/// How a <see cref="MessageBubble"/> is drawn. The theme turns it into looks through the Classes <c>style-bubble</c>,
/// <c>style-flat</c> and <c>style-lines</c>.
/// </summary>
public enum BubbleStyle
{
    /// <summary>A filled, rounded bubble around the text.</summary>
    Bubble,

    /// <summary>No bubble: the text sits on the background, as in Slack.</summary>
    Flat,

    /// <summary>One compact line per message with the time first, as in IRC.</summary>
    Lines,
}
