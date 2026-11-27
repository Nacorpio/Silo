namespace Silo.Studio;

/// <summary>How a <see cref="ReviewQuestion"/> is answered.</summary>
public enum ReviewQuestionKind
{
    /// <summary>One to five stars.</summary>
    Rating,

    /// <summary>Yes, No or Not sure, with an optional note.</summary>
    YesNo,

    /// <summary>One of the listed options.</summary>
    Choice,

    /// <summary>Free text.</summary>
    Text,
}

/// <summary>One question that the review window asks about a Control.</summary>
/// <param name="Id">The key that the answer is stored under.</param>
/// <param name="Kind">How the question is answered.</param>
/// <param name="Prompt">The question.</param>
/// <param name="Hint">What to try in the Control before answering, or an empty string.</param>
/// <param name="Options">The options of a <see cref="ReviewQuestionKind.Choice"/> question.</param>
/// <param name="Variant">The sample to show when the user asks to see what the question is about, or <see langword="null"/>.</param>
/// <param name="State">The Pseudo-state to show with it, or <see langword="null"/>.</param>
public sealed record ReviewQuestion(
    string Id,
    ReviewQuestionKind Kind,
    string Prompt,
    string Hint,
    IReadOnlyList<string> Options,
    string? Variant,
    string? State);
