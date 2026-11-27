using System.Text.Json;

namespace Silo.Studio;

/// <summary>The questions that the review window asks about one Control, read from a <c>review.jsonc</c> file.</summary>
/// <remarks>
/// <para>
/// The file is written for each new Control and has a <c>control</c> (the name of the entry in the <see cref="ControlCatalog"/>),
/// a <c>title</c>, an optional <c>pr</c>, and a list of <c>questions</c>. A question has an <c>id</c>, a <c>kind</c>
/// (<c>rating</c>, <c>yesno</c>, <c>choice</c> or <c>text</c>), a <c>prompt</c>, and optionally a <c>hint</c>, <c>options</c>,
/// <c>variant</c> and <c>state</c>. Comments and trailing commas are allowed.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var document = ReviewDocument.Parse("""{ "control": "Button", "questions": [ { "id": "q1", "kind": "rating", "prompt": "How does it feel?" } ] }""");
/// </code>
/// </example>
public sealed class ReviewDocument
{
    /// <summary>The options of the document reader.</summary>
    static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Creates a document.</summary>
    /// <param name="control">The name of the catalog entry that is reviewed.</param>
    /// <param name="title">The title of the review.</param>
    /// <param name="pr">The pull request that implements the Control, or an empty string.</param>
    /// <param name="questions">The questions.</param>
    public ReviewDocument(string control, string title, string pr, IReadOnlyList<ReviewQuestion> questions)
    {
        Control = control;
        Title = title;
        Pr = pr;
        Questions = questions;
    }

    /// <summary>Gets the name of the catalog entry that is reviewed.</summary>
    public string Control { get; }

    /// <summary>Gets the title of the review.</summary>
    public string Title { get; }

    /// <summary>Gets the pull request that implements the Control, or an empty string.</summary>
    public string Pr { get; }

    /// <summary>Gets the questions in order.</summary>
    public IReadOnlyList<ReviewQuestion> Questions { get; }

    /// <summary>Reads a document.</summary>
    /// <param name="text">The text of the file.</param>
    /// <returns>The document.</returns>
    /// <exception cref="FormatException">The text is not JSON, or a required member is missing or wrong.</exception>
    public static ReviewDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            using var json = JsonDocument.Parse(text, Options);

            var root = json.RootElement;
            var control = Required(root, "control");
            var questions = new List<ReviewQuestion>();

            if (root.TryGetProperty("questions", out var list))
                foreach (var item in list.EnumerateArray())
                    questions.Add(ReadQuestion(item));

            if (questions.Select(q => q.Id).Distinct().Count() != questions.Count)
                throw new FormatException("Two questions have the same id.");

            return new(control, Optional(root, "title") ?? $"Review of {control}", Optional(root, "pr") ?? string.Empty, questions);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            throw new FormatException(e.Message, e);
        }
    }

    /// <summary>Reads one question.</summary>
    /// <param name="item">The JSON object.</param>
    /// <returns>The question.</returns>
    static ReviewQuestion ReadQuestion(JsonElement item)
    {
        var kind = Required(item, "kind") switch
        {
            "rating" => ReviewQuestionKind.Rating,
            "yesno" => ReviewQuestionKind.YesNo,
            "choice" => ReviewQuestionKind.Choice,
            "text" => ReviewQuestionKind.Text,
            var other => throw new FormatException($"Unknown question kind '{other}'."),
        };

        var options = item.TryGetProperty("options", out var list) ? list.EnumerateArray().Select(o => o.GetString() ?? string.Empty).ToList() : [];

        if (kind == ReviewQuestionKind.Choice && options.Count < 2)
            throw new FormatException("A choice question needs at least two options.");

        return new(Required(item, "id"), kind, Required(item, "prompt"), Optional(item, "hint") ?? string.Empty, options, Optional(item, "variant"), Optional(item, "state"));
    }

    /// <summary>Gets a string member that has to be there.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member.</param>
    /// <returns>The value.</returns>
    static string Required(JsonElement element, string name)
        => Optional(element, name) is { Length: > 0 } value ? value : throw new FormatException($"The member '{name}' is missing.");

    /// <summary>Gets a string member that may be missing.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    static string? Optional(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
