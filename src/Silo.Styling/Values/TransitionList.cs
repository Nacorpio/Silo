using System.Globalization;
using System.Text;

namespace Silo.Styling.Values;

/// <summary>One entry of a <c>transition</c>: which property changes smoothly, how, and after how long.</summary>
/// <param name="Property">The kebab-case name of the property, or <c>all</c> for every property that can change smoothly.</param>
/// <param name="Motion">The duration and curve, or the spring.</param>
/// <param name="Delay">The time to wait after the change before the motion starts.</param>
public sealed record TransitionSpec(string Property, Motion Motion, TimeSpan Delay);

/// <summary>
/// What the <c>transition</c> property says: a list of properties that change smoothly instead of at once when their value changes,
/// for example <c>background 150ms ease-out, opacity 100ms 50ms</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each entry is a property name or <c>all</c>, then a duration (<c>150ms</c> or <c>0.15s</c>), then optionally an easing curve and a
/// delay (a second time). A spring is written <c>spring(170 18)</c> in place of the easing and ignores the duration, which can then
/// be left out. A later entry wins over an earlier one for the same property, and a named property wins over <c>all</c>.
/// </para>
/// </remarks>
public sealed record TransitionList
{
    /// <summary>Creates a list.</summary>
    /// <param name="entries">The entries, in the order they were written.</param>
    public TransitionList(IReadOnlyList<TransitionSpec> entries) => Entries = entries;

    /// <summary>Gets the list without any entry, which is the default: nothing changes smoothly.</summary>
    public static TransitionList None { get; } = new([]);

    /// <summary>Gets the entries, in the order they were written.</summary>
    public IReadOnlyList<TransitionSpec> Entries { get; }

    /// <summary>Finds the entry that applies to a property.</summary>
    /// <param name="property">The kebab-case name of the property.</param>
    /// <returns>The last entry that names the property, else the last <c>all</c> entry, else <see langword="null"/>.</returns>
    public TransitionSpec? Find(string property)
    {
        TransitionSpec? all = null;

        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];

            if (entry.Property == property)
                return entry;

            if (all is null && entry.Property == "all")
                all = entry;
        }

        return all;
    }

    /// <inheritdoc/>
    public bool Equals(TransitionList? other) => other is not null && Entries.SequenceEqual(other.Entries);

    /// <inheritdoc/>
    public override int GetHashCode() => Entries.Aggregate(0, (hash, entry) => HashCode.Combine(hash, entry));

    /// <summary>Writes the list the way a Stylesheet would.</summary>
    /// <returns><c>none</c> or the entries separated by commas.</returns>
    public override string ToString() => Entries.Count == 0
        ? "none"
        : string.Join(", ", Entries.Select(entry =>
            entry.Motion.Spring is { } spring
                ? $"{entry.Property} {spring} {Format(entry.Delay)}".TrimEnd()
                : $"{entry.Property} {Format(entry.Motion.Duration)} {entry.Motion.Easing} {Format(entry.Delay)}".TrimEnd()));

    /// <summary>Tries to read <c>none</c> or a comma-separated list of entries.</summary>
    /// <param name="text">The text.</param>
    /// <param name="list">The list, or <see cref="None"/> when the text could not be read.</param>
    /// <returns><see langword="false"/> for an entry without a property, an unknown word, a bad time or a third time.</returns>
    public static bool TryParse(string text, out TransitionList list)
    {
        list = None;

        text = text.Trim();

        if (text.Length == 0)
            return false;

        if (text == "none")
            return true;

        if (text.StartsWith(',') || text.EndsWith(',') || text.Contains(",,"))
            return false;

        var entries = new List<TransitionSpec>();

        foreach (var part in SplitTopLevel(text, ','))
        {
            if (!TryParseEntry(part, out var entry))
                return false;

            entries.Add(entry);
        }

        list = new TransitionList(entries);

        return true;
    }

    /// <summary>Reads one entry.</summary>
    static bool TryParseEntry(string text, out TransitionSpec entry)
    {
        entry = null!;

        string? property = null;
        Easing? easing = null;
        Spring? spring = null;
        TimeSpan? duration = null, delay = null;

        foreach (var word in SplitTopLevel(text, ' '))
        {
            if (TryTime(word, out var time))
            {
                if (duration is null)
                    duration = time;
                else if (delay is null)
                    delay = time;
                else
                    return false;
            }
            else if (word.StartsWith("spring(", StringComparison.Ordinal))
            {
                if (spring is not null || easing is not null || !Spring.TryParse(word, out var parsed))
                    return false;

                spring = parsed;
            }
            else if (Easing.TryParse(word, out var curve) && (word.Contains('(') || word is "linear" or "ease" or "ease-in" or "ease-out" or "ease-in-out"))
            {
                if (easing is not null || spring is not null)
                    return false;

                easing = curve;
            }
            else if (property is null && IsName(word))
                property = word;
            else
                return false;
        }

        if (property is null || (spring is null && duration is null))
            return false;

        var motion = spring is { } s ? new Motion(s) : new Motion(duration!.Value, easing);

        entry = new TransitionSpec(property, motion, delay ?? TimeSpan.Zero);

        return true;
    }

    /// <summary>Reads a time such as <c>150ms</c>, <c>0.2s</c> or <c>0</c>.</summary>
    static bool TryTime(string text, out TimeSpan time)
    {
        time = default;

        var (digits, scale) = text switch
        {
            _ when text.EndsWith("ms", StringComparison.Ordinal) => (text[..^2], 1.0),
            _ when text.EndsWith('s') => (text[..^1], 1000.0),
            "0" => ("0", 1.0),

            _ => (string.Empty, 0.0),
        };

        if (digits.Length == 0 || !double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0)
            return false;

        time = TimeSpan.FromMilliseconds(number * scale);

        return true;
    }

    /// <summary>Tells whether a word can be a property name.</summary>
    static bool IsName(string word) => word.Length > 0 && char.IsLetter(word[0]) && word.All(c => char.IsLetterOrDigit(c) || c == '-');

    /// <summary>Splits at a separator that is not inside parentheses.</summary>
    static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var c in text)
        {
            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;

            if (depth == 0 && (c == separator || (separator == ' ' && char.IsWhiteSpace(c))))
            {
                if (current.ToString().Trim() is { Length: > 0 } part)
                    parts.Add(part);

                current.Clear();

                continue;
            }

            current.Append(c);
        }

        if (current.ToString().Trim() is { Length: > 0 } last)
            parts.Add(last);

        return parts;
    }

    /// <summary>Writes a time, or nothing for zero.</summary>
    static string Format(TimeSpan time) => time == TimeSpan.Zero ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{time.TotalMilliseconds}ms");
}
