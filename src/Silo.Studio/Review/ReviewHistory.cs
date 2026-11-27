using System.Text.Json;

namespace Silo.Studio;

/// <summary>What earlier rounds of a review of the same Control said, so that the window can show it again.</summary>
/// <remarks>It reads the files that <see cref="ReviewAnswers.Save"/> wrote. The newest file gives the answers to show.</remarks>
/// <example>
/// <code language="csharp">
/// var history = ReviewHistory.Load("reviews", "ChatInput", commit: "105ea17 Fix the selection");
///
/// Console.WriteLine($"Round {history.Round}");
/// </code>
/// </example>
/// <param name="Round">The number of this round: one more than the number of earlier files.</param>
/// <param name="Commit">The commit that is reviewed, such as <c>105ea17 Fix the selection</c>, or <see langword="null"/>.</param>
/// <param name="Previous">The answers of the newest earlier round, by question id.</param>
public sealed record ReviewHistory(int Round, string? Commit, IReadOnlyDictionary<string, (string Value, string? Note)> Previous)
{
    /// <summary>Gets the history of a first round.</summary>
    public static ReviewHistory None { get; } = new(1, null, new Dictionary<string, (string, string?)>());

    /// <summary>Reads the earlier rounds of a Control from a folder.</summary>
    /// <param name="folder">The folder that Submit writes to. It need not exist.</param>
    /// <param name="control">The name of the Control.</param>
    /// <param name="commit">The commit that is reviewed, or <see langword="null"/> when it is not known.</param>
    /// <returns>The history. A file that cannot be read counts as a round but gives no answers.</returns>
    public static ReviewHistory Load(string folder, string control, string? commit)
    {
        if (!Directory.Exists(folder))
            return None with { Commit = commit };

        var files = Directory.GetFiles(folder, $"review-{control}-*.json");

        Array.Sort(files, StringComparer.Ordinal);

        var previous = new Dictionary<string, (string, string?)>();

        if (files.Length > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(files[^1]));

                if (document.RootElement.TryGetProperty("answers", out var answers))
                {
                    foreach (var answer in answers.EnumerateObject())
                    {
                        var value = answer.Value.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;
                        var note = answer.Value.TryGetProperty("note", out var n) ? n.GetString() : null;

                        previous[answer.Name] = (value, note);
                    }
                }
            }
            catch (Exception e) when (e is JsonException or IOException)
            {
                // A file that is half written or locked just gives no earlier answers.
            }
        }

        return new ReviewHistory(files.Length + 1, commit, previous);
    }

    /// <summary>Gets what the user said about a question last time.</summary>
    /// <param name="id">The question id.</param>
    /// <returns>For example <c>Yes: Retry did nothing</c>, or <see langword="null"/> when there was no answer.</returns>
    public string? Said(string id)
    {
        if (!Previous.TryGetValue(id, out var answer))
            return null;

        return answer.Note is null ? answer.Value : $"{answer.Value}: {answer.Note}";
    }
}
