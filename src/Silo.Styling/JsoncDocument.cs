using System.Globalization;
using System.Text;

namespace Silo.Styling;

/// <summary>
/// A JSONC Stylesheet as a lossless syntax tree: the text exactly as written, with the position of every object, member and
/// value, so tools can change a Stylesheet and keep its comments, white space, ordering and trailing commas.
/// </summary>
/// <remarks>
/// <para>
/// This is the editable form of the JSONC format (ADR 0002). <see cref="Parse"/> reads the text and <see cref="ToString"/>
/// returns it unchanged. An edit such as <see cref="SetValue"/> splices only the text that has to change and returns a new
/// document, so the old one stays valid and an editor can keep it for undo.
/// </para>
/// <para>
/// Members are addressed by a path of keys from the root, such as <c>["rules", "Button", "background"]</c>. When a key is
/// written twice the last member wins, the same as when the Stylesheet is applied. A nested Rule is a member of its parent's
/// object, so <c>"&amp;:hover"</c> is addressed under the Rule that holds it, not as <c>Button:hover</c>.
/// </para>
/// <para>
/// The tree accepts the same text as <see cref="JsoncStylesheetReader"/> apart from checks on the meaning of keys; arrays
/// are rejected, since the Stylesheet format has none. Every edit parses its result again, so a document can never become
/// invalid.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var document = JsoncDocument.Parse("""
///     {
///       "rules": {
///         // The primary button.
///         "Button": { "background": "#3b82f6", },
///       },
///     }
///     """, "theme.jsonc");
///
/// var edited = document.SetValue(["rules", "Button", "background"], "#2563eb")
///                      .SetValue(["rules", "Button", "color"], "white");
///
/// edited.Text;   // the comment, the commas and the layout are unchanged; the new member follows the last one
/// </code>
/// </example>
public sealed class JsoncDocument
{
    /// <summary>The number of spaces to indent by when the document has no nested lines to copy the indentation of.</summary>
    const int DefaultIndent = 2;

    /// <summary>Creates a document from parsed parts.</summary>
    /// <param name="text">The text.</param>
    /// <param name="fileName">The file name for positions.</param>
    /// <param name="root">The root object.</param>
    JsoncDocument(string text, string fileName, JsoncObject root)
    {
        Text = text;
        FileName = fileName;
        Root = root;
    }


    /// <summary>Gets the text of the document, exactly as it was read or edited.</summary>
    public string Text { get; }

    /// <summary>Gets the name that positions and error messages report.</summary>
    public string FileName { get; }

    /// <summary>Gets the root object, which holds <c>tokens</c> and <c>rules</c>.</summary>
    public JsoncObject Root { get; }

    /// <summary>Reads JSONC text into a syntax tree.</summary>
    /// <param name="text">The Stylesheet text.</param>
    /// <param name="fileName">The name to put in positions and error messages.</param>
    /// <returns>The document. Its <see cref="Text"/> is <paramref name="text"/>.</returns>
    /// <exception cref="StylesheetException">
    /// The text is not valid JSONC with an object at the root. The message starts with <c>fileName(line,column):</c>.
    /// </exception>
    public static JsoncDocument Parse(string text, string fileName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fileName);

        return new(text, fileName, new JsoncParser(text, fileName).ParseRoot());
    }

    /// <summary>Returns the text of the document.</summary>
    /// <returns><see cref="Text"/>.</returns>
    public override string ToString() => Text;

    /// <summary>Converts an offset in the text into a line and column.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The position, with line and column counted from 1.</returns>
    public SourceSpan PositionOf(int offset) => Position(Text, FileName, offset);

    /// <summary>Reads the document as a <see cref="Stylesheet"/>.</summary>
    /// <returns>The Stylesheet.</returns>
    /// <exception cref="StylesheetException">The text is valid JSONC but not a valid Stylesheet.</exception>
    public Stylesheet ToStylesheet() => JsoncStylesheetReader.Read(Text, FileName);

    /// <summary>Finds a member by its path.</summary>
    /// <param name="path">The keys from the root.</param>
    /// <returns>The member, or <see langword="null"/> when the path does not lead to one.</returns>
    public JsoncMember? Find(params string[] path)
    {
        var current = Root;
        JsoncMember? member = null;

        foreach (var key in path)
        {
            if (current is null)
                return null;

            member = LastMember(current, key);

            if (member is null)
                return null;

            current = member.Value as JsoncObject;
        }

        return member;
    }

    /// <summary>Sets the value of a member to a string, adding the member and any objects above it that are missing.</summary>
    /// <param name="path">The keys from the root. The last one is the member that gets the value.</param>
    /// <param name="value">The new value, written as a JSON string.</param>
    /// <returns>The edited document.</returns>
    /// <exception cref="InvalidOperationException">The path leads to an object, which a value would replace.</exception>
    public JsoncDocument SetValue(IReadOnlyList<string> path, string value) => SetRawValue(path, Quote(value));

    /// <summary>Sets the value of a member to JSON text that is written as given, such as a number.</summary>
    /// <param name="path">The keys from the root.</param>
    /// <param name="json">The JSON text of a scalar.</param>
    /// <returns>The edited document.</returns>
    /// <exception cref="InvalidOperationException">The path leads to an object, which a value would replace.</exception>
    public JsoncDocument SetRawValue(IReadOnlyList<string> path, string json)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(json);

        if (path.Count == 0)
            throw new ArgumentException("A path needs at least one key.", nameof(path));

        var owner = Root;
        var depth = 0;

        while (depth < path.Count - 1 && LastMember(owner, path[depth]) is { } member)
        {
            var message = $"'{path[depth]}' is a value, so '{path[depth + 1]}' cannot be inside it.";

            owner = member.Value as JsoncObject ?? throw new InvalidOperationException(message);

            depth++;
        }

        var existing = depth == path.Count - 1 ? LastMember(owner, path[depth]) : null;

        if (existing is null)
            return AddMember(owner, [.. path.Skip(depth)], json);

        if (existing.Value is JsoncObject)
            throw new InvalidOperationException($"'{path[^1]}' is an object, not a value.");

        return Splice(existing.Value.Start, existing.Value.End, json);
    }

    /// <summary>Removes a member, with its comma and the line it is on when it is alone there.</summary>
    /// <param name="path">The keys from the root.</param>
    /// <returns>The edited document, or this document when there is no such member.</returns>
    /// <remarks>Comments on their own lines above the member stay where they are.</remarks>
    public JsoncDocument Remove(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var member = Find([.. path]);

        if (member is null)
            return this;

        var parent = path.Count == 1 ? Root : (JsoncObject)Find([.. path.Take(path.Count - 1)])!.Value;
        var (start, end) = RemovalRange(parent, member);

        return Splice(start, end, string.Empty);
    }

    /// <summary>Quotes text as a JSON string.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The string with quotes and escapes, which is what a Stylesheet value looks like in the file.</returns>
    public static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2).Append('"');

        foreach (var c in value)
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;

                case < ' ':
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;

                default: sb.Append(c); break;
            }

        return sb.Append('"').ToString();
    }

    /// <summary>Gets the last member with a key, which is the one that wins.</summary>
    /// <param name="owner">The object.</param>
    /// <param name="key">The key.</param>
    /// <returns>The member, or <see langword="null"/>.</returns>
    static JsoncMember? LastMember(JsoncObject owner, string key) => owner.Members.LastOrDefault(m => m.Key == key);

    /// <summary>Converts an offset into a line and column.</summary>
    /// <param name="text">The text.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The position; the column counts characters, as in <see cref="JsoncStylesheetReader"/>.</returns>
    static SourceSpan Position(string text, string fileName, int offset)
    {
        var line = 1;
        var column = 1;

        for (var i = 0; i < offset && i < text.Length; i++)
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
                column++;

        return new(fileName, line, column);
    }

    /// <summary>Returns a document with a range of the text replaced.</summary>
    /// <param name="start">The first offset to replace.</param>
    /// <param name="end">The offset after the last one to replace.</param>
    /// <param name="replacement">The new text.</param>
    /// <returns>The new document, which is parsed again.</returns>
    JsoncDocument Splice(int start, int end, string replacement)
        => Parse(string.Concat(Text.AsSpan(0, start), replacement, Text.AsSpan(end)), FileName);

    /// <summary>Returns a document with text inserted at two offsets.</summary>
    /// <param name="first">The first offset.</param>
    /// <param name="firstText">The text for the first offset.</param>
    /// <param name="second">The second offset, which is not before the first.</param>
    /// <param name="secondText">The text for the second offset.</param>
    /// <returns>The new document.</returns>
    JsoncDocument Insert(int first, string firstText, int second, string secondText)
        => Parse(Text[..first] + firstText + Text[first..second] + secondText + Text[second..], FileName);

    /// <summary>Adds a chain of members at the end of an object.</summary>
    /// <param name="owner">The object that gets the first member of the chain.</param>
    /// <param name="chain">The keys. All but the last are objects that are created.</param>
    /// <param name="json">The value of the last key.</param>
    /// <returns>The edited document.</returns>
    JsoncDocument AddMember(JsoncObject owner, IReadOnlyList<string> chain, string json)
    {
        var inline = !Text.AsSpan(owner.Start, owner.End - owner.Start).Contains('\n');

        if (owner.Members.Count == 0)
            return AddToEmpty(owner, chain, json, inline);

        var last = owner.Members[^1];
        var after = last.Comma >= 0 ? last.Comma + 1 : last.End;

        if (inline)
        {
            var separator = last.Comma >= 0 ? " " : ", ";
            var trailing = last.Comma >= 0 ? "," : string.Empty;

            var inlineMember = MemberText(chain, json, string.Empty, string.Empty, inline: true);

            return Splice(after, after, separator + inlineMember + trailing);
        }

        var indent = LineIndent(last.Start);
        var newline = NewLine;
        var member = newline + indent + MemberText(chain, json, indent, IndentUnit(), inline: false, newline);
        var lineEnd = EndOfLine(after);

        // The new member keeps the style of the old last one: a trailing comma gives it a trailing comma, and without
        // one the old last member gets the comma that separates it from the new one.
        return last.Comma >= 0
            ? Splice(lineEnd, lineEnd, member + ",")
            : Insert(last.End, ",", lineEnd, member);
    }

    /// <summary>Adds the first member to an object that has none.</summary>
    /// <param name="owner">The empty object.</param>
    /// <param name="chain">The keys.</param>
    /// <param name="json">The value of the last key.</param>
    /// <param name="inline">Whether the object is written on one line.</param>
    /// <returns>The edited document.</returns>
    JsoncDocument AddToEmpty(JsoncObject owner, IReadOnlyList<string> chain, string json, bool inline)
    {
        if (inline)
        {
            var single = MemberText(chain, json, string.Empty, string.Empty, inline: true);

            return Splice(owner.Start + 1, owner.End - 1, " " + single + " ");
        }

        var braceIndent = LineIndent(owner.Start);
        var indent = braceIndent + IndentUnit();
        var newline = NewLine;
        var member = MemberText(chain, json, indent, IndentUnit(), inline: false, newline);
        var text = newline + indent + member + newline + braceIndent;

        return Splice(owner.Start + 1, owner.End - 1, text);
    }

    /// <summary>Writes a member, with its nested objects when the chain has more than one key.</summary>
    /// <param name="chain">The keys.</param>
    /// <param name="json">The value of the last key.</param>
    /// <param name="indent">The indentation of the member's line.</param>
    /// <param name="unit">The indentation of one level.</param>
    /// <param name="inline">Whether to write it on one line.</param>
    /// <returns>The member text without a trailing comma.</returns>
    /// <param name="newline">The line break that the document uses.</param>
    static string MemberText(
        IReadOnlyList<string> chain, string json, string indent, string unit, bool inline, string newline = "\n")
    {
        var key = Quote(chain[0]);

        if (chain.Count == 1)
            return $"{key}: {json}";

        var inner = MemberText([.. chain.Skip(1)], json, indent + unit, unit, inline, newline);

        return inline
            ? $"{key}: {{ {inner} }}"
            : $"{key}: {{{newline}{indent}{unit}{inner}{newline}{indent}}}";
    }

    /// <summary>Gets the line break that the document uses: <c>\r\n</c> when it has one, otherwise <c>\n</c>.</summary>
    string NewLine => Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>Gets the white space at the start of the line that an offset is on.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The spaces and tabs before the first other character of the line.</returns>
    string LineIndent(int offset)
    {
        var start = Text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;

        if (offset == 0)
            start = 0;

        var end = start;

        while (end < Text.Length && Text[end] is ' ' or '\t')
            end++;

        return Text[start..end];
    }

    /// <summary>Gets the indentation of one level, copied from the first member of the root.</summary>
    /// <returns>The white space, or two spaces when the document has no indented member to copy it from.</returns>
    string IndentUnit()
    {
        if (Root.Members.Count == 0)
            return new(' ', DefaultIndent);

        var unit = LineIndent(Root.Members[0].Start)[LineIndent(Root.Start).Length..];

        return unit.Length == 0 ? new(' ', DefaultIndent) : unit;
    }

    /// <summary>Finds where the line that an offset is on ends, counting a comment after the offset as part of the line.</summary>
    /// <param name="offset">The offset, usually just after a member and its comma.</param>
    /// <returns>The offset after the last character of the line's comment, or <paramref name="offset"/> when there is none.</returns>
    int EndOfLine(int offset)
    {
        var end = offset;
        var position = offset;

        while (position < Text.Length)
        {
            if (Text[position] is ' ' or '\t')
            {
                position++;
                continue;
            }

            if (Text.AsSpan(position).StartsWith("//"))
            {
                var newline = Text.IndexOf('\n', position);

                return newline < 0 ? Text.Length : TrimCarriageReturn(newline);
            }

            if (!Text.AsSpan(position).StartsWith("/*"))
                break;

            var close = Text.IndexOf("*/", position + 2, StringComparison.Ordinal);

            if (close < 0 || Text.AsSpan(position, close - position).Contains('\n'))
                break;

            position = close + 2;
            end = position;
        }

        return end;
    }

    /// <summary>Moves back over a carriage return that precedes a line feed.</summary>
    /// <param name="newline">The offset of the line feed.</param>
    /// <returns>The offset where the line's text ends.</returns>
    int TrimCarriageReturn(int newline) => newline > 0 && Text[newline - 1] == '\r' ? newline - 1 : newline;

    /// <summary>Works out what text to cut to remove a member.</summary>
    /// <param name="parent">The object that holds the member.</param>
    /// <param name="member">The member.</param>
    /// <returns>The range to remove.</returns>
    (int Start, int End) RemovalRange(JsoncObject parent, JsoncMember member)
    {
        var end = member.Comma >= 0 ? member.Comma + 1 : member.End;
        var lineStart = Text.LastIndexOf('\n', Math.Max(0, member.Start - 1)) + 1;
        var alone = member.Start > 0 && Text.AsSpan(lineStart, member.Start - lineStart).Trim(" \t").IsEmpty;
        var lineEnd = EndOfLine(end);
        var newlineAfter = Text.IndexOf('\n', lineEnd);
        var restEnd = newlineAfter < 0 ? Text.Length : newlineAfter;
        var restIsEmpty = Text.AsSpan(lineEnd, restEnd - lineEnd).Trim(" \t\r").IsEmpty;

        if (alone && restIsEmpty)
        {
            var newline = Text.IndexOf('\n', lineEnd);

            return (lineStart, newline < 0 ? Text.Length : newline + 1);
        }

        if (member.Comma >= 0)
        {
            var next = end;

            while (next < Text.Length && Text[next] is ' ' or '\t')
                next++;

            return (member.Start, next);
        }

        var index = parent.Members.ToList().IndexOf(member);

        return index > 0 && parent.Members[index - 1].Comma >= 0
            ? (parent.Members[index - 1].Comma, member.End)
            : (member.Start, member.End);
    }
}
