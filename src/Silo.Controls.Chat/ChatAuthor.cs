using Silo.Rendering;

namespace Silo;

/// <summary>
/// The person or agent that wrote a <see cref="ChatMessage"/>.
/// </summary>
/// <param name="Name">The name that is shown above the message.</param>
/// <param name="Avatar">The picture, or <see langword="null"/> to show the initials of the name.</param>
/// <param name="IsSelf">Whether this is the person using the app, whose messages are aligned and colored as their own.</param>
/// <example>
/// <code language="csharp">
/// var me = new ChatAuthor("Nac", IsSelf: true);
/// var dana = new ChatAuthor("Dana");
/// </code>
/// </example>
public sealed record ChatAuthor(string Name, IImage? Avatar = null, bool IsSelf = false);
