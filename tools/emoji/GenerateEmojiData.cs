// Builds src/Silo.Controls/Emoji/emoji.tsv, the table behind Silo.EmojiCatalog.
//
// Inputs (download them, then pass the paths):
//   emoji-test.txt   https://unicode.org/Public/emoji/16.0/emoji-test.txt
//   annotations.json https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-annotations-full/annotations/en/annotations.json
//
// Run:  dotnet run tools/emoji/GenerateEmojiData.cs -- <emoji-test.txt> <annotations.json> src/Silo.Controls/Emoji/emoji.tsv
//
// Each line of the output is: group, emoji, name, keywords (separated by |), five skin tone variants (separated by a space).
//
// What is left out, so that the picker stays small:
//   - Entries that are not fully-qualified, and the Component group.
//   - Gender and family sequences (a zero width joiner sequence that starts with man or woman, the family subgroup, kiss and couple
//     with heart sequences). The gender neutral person form stays.
//   - Skin tone variants that are not a single tone on a single person. A tone variant is attached to its base emoji instead of listed.
using System.Globalization;
using System.Text;
using System.Text.Json;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: GenerateEmojiData <emoji-test.txt> <annotations.json> <output.tsv>");

    return 1;
}

var tones = new[] { "light", "medium-light", "medium", "medium-dark", "dark" };
var keywords = new Dictionary<string, List<string>>();

using (var json = JsonDocument.Parse(File.ReadAllText(args[1])))
{
    foreach (var entry in json.RootElement.GetProperty("annotations").GetProperty("annotations").EnumerateObject())
        if (entry.Value.TryGetProperty("default", out var words))
            keywords[entry.Name] = [.. words.EnumerateArray().Select(w => w.GetString()!)];
}

var rows = new List<Row>();
var group = string.Empty;
var subgroup = string.Empty;

foreach (var line in File.ReadLines(args[0]))
{
    if (line.StartsWith("# group: "))
    {
        group = line["# group: ".Length..].Trim();

        continue;
    }

    if (line.StartsWith("# subgroup: "))
    {
        subgroup = line["# subgroup: ".Length..].Trim();

        continue;
    }

    if (line.Length == 0 || line[0] == '#' || group == "Component" || !line.Contains("; fully-qualified"))
        continue;

    var comment = line[(line.IndexOf('#') + 1)..].Trim();
    var space = comment.IndexOf(' ');
    var emoji = comment[..space];
    var rest = comment[(space + 1)..].Trim();
    var name = rest[(rest.IndexOf(' ') + 1)..].Trim();

    rows.Add(new Row(group, subgroup, emoji, name));
}

static bool IsTone(int codePoint) => codePoint is >= 0x1F3FB and <= 0x1F3FF;

static int ToneCount(string emoji) => emoji.EnumerateRunes().Count(r => IsTone(r.Value));

static bool HasJoiner(string emoji) => emoji.Contains('‍');

var bases = new Dictionary<string, Row>();
var variants = new Dictionary<string, string[]>();

foreach (var row in rows.Where(r => ToneCount(r.Emoji) == 0))
{
    var gendered = HasJoiner(row.Emoji) && (row.Name.StartsWith("man ") || row.Name.StartsWith("man:") || row.Name.StartsWith("woman ")
        || row.Name.StartsWith("woman:") || row.Subgroup == "family" || row.Name.StartsWith("kiss:") || row.Name.StartsWith("couple with heart:"));

    if (!gendered)
        bases[row.Name] = row;
}

foreach (var row in rows.Where(r => ToneCount(r.Emoji) == 1))
{
    var index = tones.ToList().FindIndex(t => row.Name.EndsWith($": {t} skin tone"));

    if (index < 0)
        continue;

    var baseName = row.Name[..row.Name.LastIndexOf(": ", StringComparison.Ordinal)];

    if (!bases.ContainsKey(baseName))
        continue;

    if (!variants.TryGetValue(baseName, out var list))
        variants[baseName] = list = new string[5];

    list[index] = row.Emoji;
}

var output = new StringBuilder();
var kept = 0;

foreach (var row in rows.Where(r => bases.TryGetValue(r.Name, out var b) && b == r))
{
    var words = keywords.TryGetValue(row.Emoji, out var found) ? found
        : keywords.TryGetValue(row.Emoji.Replace("️", string.Empty), out found) ? found : [];
    var full = variants.TryGetValue(row.Name, out var tonesOfRow) && tonesOfRow.All(v => v is not null) ? string.Join(' ', tonesOfRow) : string.Empty;

    output.Append(row.Group).Append('\t').Append(row.Emoji).Append('\t').Append(row.Name).Append('\t')
        .Append(string.Join('|', words.Distinct())).Append('\t').Append(full).Append('\n');

    kept++;
}

File.WriteAllText(args[2], output.ToString(), new UTF8Encoding(false));

Console.WriteLine($"{kept} emoji written to {args[2]}");

return 0;

record Row(string Group, string Subgroup, string Emoji, string Name);
