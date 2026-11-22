using System.Globalization;
using System.Text;

namespace Silo.Styling;

/// <summary>Reads JSONC text into the nodes of a <see cref="JsoncDocument"/>, keeping the offset of every node.</summary>
/// <remarks>
/// It accepts comments, trailing commas and duplicate keys, which is what <see cref="JsoncStylesheetReader"/> accepts, and
/// rejects arrays since a Stylesheet has none. Errors are reported as <see cref="StylesheetException"/> with the position.
/// </remarks>
/// <param name="text">The text to read.</param>
/// <param name="fileName">The name that error positions report.</param>
sealed class JsoncParser(string text, string fileName)
{
    /// <summary>The offset of the next character to read.</summary>
    int position;

    /// <summary>Reads the whole text, which must be one object.</summary>
    /// <returns>The root object.</returns>
    public JsoncObject ParseRoot()
    {
        SkipTrivia();

        if (Peek() != '{')
            throw Fail(position, "Expected '{' at the start of the Stylesheet.");

        var root = ParseObject();

        SkipTrivia();

        if (position < text.Length)
            throw Fail(position, "Unexpected text after the Stylesheet.");

        return root;
    }

    /// <summary>Gets the character at the current offset.</summary>
    /// <returns>The character, or a null character at the end of the text.</returns>
    char Peek() => position < text.Length ? text[position] : '\0';

    /// <summary>Creates an error with the position of an offset in its message.</summary>
    /// <param name="offset">The offset.</param>
    /// <param name="message">The description.</param>
    /// <returns>The exception.</returns>
    StylesheetException Fail(int offset, string message)
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

        return new($"{fileName}({line},{column}): {message}");
    }

    /// <summary>Moves over white space and comments.</summary>
    void SkipTrivia()
    {
        while (position < text.Length)
        {
            var c = text[position];

            if (char.IsWhiteSpace(c))
            {
                position++;
                continue;
            }

            if (c != '/' || position + 1 >= text.Length)
                return;

            if (text[position + 1] == '/')
            {
                var newline = text.IndexOf('\n', position);

                position = newline < 0 ? text.Length : newline + 1;
                continue;
            }

            if (text[position + 1] != '*')
                return;

            var close = text.IndexOf("*/", position + 2, StringComparison.Ordinal);

            if (close < 0)
                throw Fail(position, "The comment is not closed.");

            position = close + 2;
        }
    }

    /// <summary>Reads a value.</summary>
    /// <returns>The value.</returns>
    JsoncValue ParseValue()
    {
        var c = Peek();

        if (c == '{')
            return ParseObject();

        if (c == '[')
            throw Fail(position, "Arrays are not part of the Stylesheet format.");

        if (c == '"')
            return ParseString();

        return ParseWord();
    }

    /// <summary>Reads an object.</summary>
    /// <returns>The object.</returns>
    JsoncObject ParseObject()
    {
        var start = position;
        var members = new List<JsoncMember>();

        position++;

        while (true)
        {
            SkipTrivia();

            if (Peek() == '}')
            {
                position++;

                return new(start, position, members);
            }

            if (Peek() != '"')
                throw Fail(position, "Expected a key or '}'.");

            var key = ParseString();

            SkipTrivia();

            if (Peek() != ':')
                throw Fail(position, "Expected ':' after the key.");

            position++;
            SkipTrivia();

            var value = ParseValue();

            SkipTrivia();

            var comma = -1;

            if (Peek() == ',')
            {
                comma = position;
                position++;
            }

            members.Add(new(key.Start, key.End, key.Value, value, comma));

            if (comma < 0)
            {
                SkipTrivia();

                if (Peek() != '}')
                    throw Fail(position, "Expected ',' or '}'.");
            }
        }
    }

    /// <summary>Reads a string with its escapes.</summary>
    /// <returns>The string scalar.</returns>
    JsoncScalar ParseString()
    {
        var start = position;
        var sb = new StringBuilder();

        position++;

        while (position < text.Length && text[position] != '"')
        {
            var c = text[position];

            if (c is '\n' or '\r')
                throw Fail(start, "The string is not closed.");

            position++;

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            sb.Append(ParseEscape());
        }

        if (position >= text.Length)
            throw Fail(start, "The string is not closed.");

        position++;

        return new(start, position, JsoncScalarKind.String, text[start..position], sb.ToString());
    }

    /// <summary>Reads the character after a backslash.</summary>
    /// <returns>The text that the escape stands for.</returns>
    string ParseEscape()
    {
        if (position >= text.Length)
            throw Fail(position, "The escape is not finished.");

        var c = text[position++];

        switch (c)
        {
            case '"': return "\"";
            case '\\': return "\\";
            case '/': return "/";
            case 'b': return "\b";
            case 'f': return "\f";
            case 'n': return "\n";
            case 'r': return "\r";
            case 't': return "\t";

            case 'u':
                var digits = text.AsSpan(position, Math.Min(4, text.Length - position));

                if (digits.Length < 4 || !int.TryParse(digits, NumberStyles.HexNumber, null, out var code))
                    throw Fail(position - 2, "A \\u escape needs four hexadecimal digits.");

                position += 4;

                return ((char)code).ToString();

            default:
                throw Fail(position - 2, $"'\\{c}' is not a valid escape.");
        }
    }

    /// <summary>Tells whether a character can be part of a number, <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter, digit, sign or point.</returns>
    static bool IsWordCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '+' or '.';

    /// <summary>Reads a number, <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    /// <returns>The scalar.</returns>
    JsoncScalar ParseWord()
    {
        var start = position;

        while (position < text.Length && IsWordCharacter(text[position]))
            position++;

        var word = text[start..position];

        return word switch
        {
            "true" or "false" => new(start, position, JsoncScalarKind.Boolean, word, word),
            "null" => new(start, position, JsoncScalarKind.Null, word, word),
            _ when double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                => new(start, position, JsoncScalarKind.Number, word, word),

            _ => throw Fail(start, word.Length == 0 ? "Expected a value." : $"'{word}' is not a valid value."),
        };
    }
}
