namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Chat category.</summary>
    static IEnumerable<CatalogEntry> Chat() =>
    [
        new("ChatInput", "Chat", CreateChatInput),
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
}
