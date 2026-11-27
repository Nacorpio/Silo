namespace Silo;

/// <summary>When a <see cref="ChatInput"/> shows its Send button.</summary>
public enum SendButtonMode
{
    /// <summary>The button is always there.</summary>
    Always,

    /// <summary>The button appears while there is text to send.</summary>
    WhenText,

    /// <summary>There is no button. Enter sends.</summary>
    Never,
}
