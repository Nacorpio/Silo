using System.Text.Json;

namespace Silo.Studio;

/// <summary>What the user has answered in a review, and the part of the Control that they looked at.</summary>
/// <remarks>The review window fills it while the user answers. <see cref="ToJson"/> gives the file that <see cref="Save"/> writes.</remarks>
/// <example>
/// <code language="csharp">
/// var answers = new ReviewAnswers("ToolCallCard");
///
/// answers.Set("feel", "4", note: null);
/// answers.Comment = "The chevron is small.";
/// </code>
/// </example>
/// <param name="control">The name of the reviewed Control.</param>
public sealed class ReviewAnswers(string control)
{
    /// <summary>The answers by question id.</summary>
    readonly Dictionary<string, (string Value, string? Note)> answers = [];

    /// <summary>The samples that were shown, in the order they were first shown.</summary>
    readonly List<string> viewed = [];

    /// <summary>Gets the name of the reviewed Control.</summary>
    public string Control { get; } = control;

    /// <summary>Gets or sets the free comment about the whole Control.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the user wants the whole iteration thrown away and done again, instead of improved. The comment is then
    /// the reason. The default is <see langword="false"/>.
    /// </summary>
    public bool Redo { get; set; }

    /// <summary>Gets the number of questions that have an answer.</summary>
    public int Count => answers.Count;

    /// <summary>Gets the samples that were shown, as <c>variant/state</c>.</summary>
    public IReadOnlyList<string> Viewed => viewed;

    /// <summary>Stores an answer, or removes it when the value is empty.</summary>
    /// <param name="id">The question id.</param>
    /// <param name="value">The answer.</param>
    /// <param name="note">A note that goes with the answer, or <see langword="null"/>.</param>
    public void Set(string id, string value, string? note)
    {
        if (value.Length == 0 && string.IsNullOrWhiteSpace(note))
            answers.Remove(id);
        else
            answers[id] = (value, string.IsNullOrWhiteSpace(note) ? null : note);
    }

    /// <summary>Gets an answer.</summary>
    /// <param name="id">The question id.</param>
    /// <returns>The answer, or <see langword="null"/> when there is none.</returns>
    public string? Get(string id) => answers.TryGetValue(id, out var answer) ? answer.Value : null;

    /// <summary>Notes that a sample was shown.</summary>
    /// <param name="variant">The variant.</param>
    /// <param name="state">The Pseudo-state.</param>
    public void MarkViewed(string variant, string state)
    {
        var key = $"{variant}/{state}";

        if (!viewed.Contains(key))
            viewed.Add(key);
    }

    /// <summary>Writes the answers as JSON.</summary>
    /// <param name="at">The time to record.</param>
    /// <returns>The JSON text.</returns>
    public string ToJson(DateTimeOffset at)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("control", Control);
            writer.WriteString("submitted", at);
            writer.WriteString("verdict", Redo ? "redo" : "keep");
            writer.WriteString("comment", Comment);
            writer.WriteStartArray("viewed");

            foreach (var item in viewed)
                writer.WriteStringValue(item);

            writer.WriteEndArray();
            writer.WriteStartObject("answers");

            foreach (var (id, (value, note)) in answers)
            {
                writer.WriteStartObject(id);
                writer.WriteString("value", value);

                if (note is not null)
                    writer.WriteString("note", note);

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Writes the answers to <c>review-&lt;control&gt;-&lt;timestamp&gt;.json</c> in a folder.</summary>
    /// <param name="folder">The folder, which is created when it does not exist.</param>
    /// <param name="at">The time to record and to put in the name.</param>
    /// <returns>The path of the file.</returns>
    public string Save(string folder, DateTimeOffset at)
    {
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"review-{Control}-{at:yyyyMMdd-HHmmss}.json");

        File.WriteAllText(path, ToJson(at));

        return path;
    }
}
