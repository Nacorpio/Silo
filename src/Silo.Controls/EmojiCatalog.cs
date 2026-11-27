namespace Silo;

/// <summary>
/// The emoji that the <see cref="EmojiPanel"/> offers, and a way to search them.
/// </summary>
/// <remarks>
/// <para>
/// The data is Unicode 16.0 <c>emoji-test</c> plus the English CLDR keywords, and is read from a resource of the assembly the first
/// time it is needed. Gender and family sequences are left out and skin tones are attached to their base emoji: see
/// <c>tools/emoji/GenerateEmojiData.cs</c>, which makes the resource.
/// </para>
/// <para>
/// The catalog is also what shortcode completion such as <c>:smi</c> searches, so there is one copy of the data.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// foreach (var emoji in EmojiCatalog.Find("smile").Take(5))
///     Console.WriteLine($"{emoji.Value} {emoji.Name}");
/// </code>
/// </example>
public static class EmojiCatalog
{
    static readonly Lazy<(IReadOnlyList<EmojiEntry> All, IReadOnlyList<string> Groups, Dictionary<string, EmojiEntry> ByValue)> Data = new(Load);

    /// <summary>Gets every emoji, in the order that Unicode lists them.</summary>
    public static IReadOnlyList<EmojiEntry> All => Data.Value.All;

    /// <summary>Gets the group names, in the order that Unicode lists them.</summary>
    public static IReadOnlyList<string> Groups => Data.Value.Groups;

    /// <summary>Finds the entry of an emoji.</summary>
    /// <param name="value">The emoji, in any skin tone.</param>
    /// <returns>The entry, or <see langword="null"/> when the catalog does not have it.</returns>
    public static EmojiEntry? Get(string value)
        => Data.Value.ByValue.TryGetValue(value, out var entry) ? entry : null;

    /// <summary>Searches the names and keywords.</summary>
    /// <param name="query">The words to look for. Every word must match the start of a word of the name or of a keyword, or sit inside the name. A leading colon is ignored, so <c>":smi"</c> works.</param>
    /// <returns>The matches. Names that start with the query come first, then names with a word that starts with it, then keywords, then the rest, each in catalog order. An empty query matches nothing.</returns>
    public static IReadOnlyList<EmojiEntry> Find(string query)
    {
        var words = query.Trim().TrimStart(':').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return [];

        var hits = new List<(int Rank, int Index, EmojiEntry Entry)>();
        var phrase = string.Join(' ', words);

        for (var i = 0; i < All.Count; i++)
        {
            var entry = All[i];
            var rank = RankOf(entry, phrase, words);

            if (rank >= 0)
                hits.Add((rank, i, entry));
        }

        return [.. hits.OrderBy(h => h.Rank).ThenBy(h => h.Index).Select(h => h.Entry)];
    }

    /// <summary>Ranks how well an entry matches a query.</summary>
    /// <param name="entry">The emoji.</param>
    /// <param name="phrase">The whole query, lower case.</param>
    /// <param name="words">The words of the query.</param>
    /// <returns>0 when the name starts with the query, 1 when a word of the name starts with every query word, 2 when keywords are needed, 3 for the rest, or -1 for no match.</returns>
    static int RankOf(EmojiEntry entry, string phrase, string[] words)
    {
        if (entry.Name.StartsWith(phrase, StringComparison.Ordinal))
            return 0;

        var nameWords = entry.Name.Split(' ', ':', ',');
        var worst = 1;

        foreach (var word in words)
        {
            if (nameWords.Any(n => n.StartsWith(word, StringComparison.Ordinal)))
                continue;

            if (entry.Keywords.Any(k => k.StartsWith(word, StringComparison.Ordinal)))
                worst = Math.Max(worst, 2);
            else if (entry.Name.Contains(word, StringComparison.Ordinal))
                worst = Math.Max(worst, 3);
            else
                return -1;
        }

        return worst;
    }

    static (IReadOnlyList<EmojiEntry>, IReadOnlyList<string>, Dictionary<string, EmojiEntry>) Load()
    {
        using var stream = typeof(EmojiCatalog).Assembly.GetManifestResourceStream("Silo.Emoji.emoji.tsv")
            ?? throw new InvalidOperationException("The emoji resource is missing from Silo.Controls.");
        using var reader = new StreamReader(stream);

        var all = new List<EmojiEntry>();
        var groups = new List<string>();
        var byValue = new Dictionary<string, EmojiEntry>();

        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var columns = line.Split('\t');
            var tones = columns[4].Length == 0 ? [] : columns[4].Split(' ');
            var keywords = columns[3].Length == 0 ? [] : columns[3].Split('|');
            var entry = new EmojiEntry(columns[1], columns[2].ToLowerInvariant(), columns[0], keywords, tones);

            if (!groups.Contains(entry.Group))
                groups.Add(entry.Group);

            all.Add(entry);
            byValue[entry.Value] = entry;

            foreach (var tone in tones)
                byValue[tone] = entry;
        }

        return (all, groups, byValue);
    }
}
