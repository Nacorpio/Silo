namespace Silo;

/// <summary>
/// A file that a <see cref="ChatMessage"/> carries. A bubble lists its names; a template can show more.
/// </summary>
/// <param name="Name">The file name.</param>
/// <param name="MediaType">The media type, such as <c>image/png</c>, or <see langword="null"/> when it is not known.</param>
public sealed record ChatAttachment(string Name, string? MediaType = null);
