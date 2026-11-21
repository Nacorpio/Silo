using System.Text;
using System.Text.Json;

namespace Silo.Styling;

/// <summary>Reads the JSONC Stylesheet format into the syntax-independent <see cref="Stylesheet"/> model.</summary>
public static class JsoncStylesheetReader
{
    static readonly JsonReaderOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static Stylesheet Read(string json, string fileName)
    {
        var bytes = new UTF8Encoding(false).GetBytes(json);
        var r = new Parser(bytes, fileName);
        try { return r.ReadSheet(); }
        catch (JsonException ex) { throw new StylesheetException($"{fileName}({ex.LineNumber + 1},{ex.BytePositionInLine + 1}): {ex.Message}"); }
    }

    ref struct Parser(byte[] bytes, string file)
    {
        Utf8JsonReader _r = new(bytes, Options);
        readonly List<Rule> _rules = [];
        readonly Dictionary<string, TokenDeclaration> _tokens = new(StringComparer.Ordinal);

        SourceSpan Here(long offset)
        {
            var line = 1; var col = 1;
            for (var i = 0; i < offset && i < bytes.Length; i++)
            {
                if (bytes[i] == '\n') { line++; col = 1; }
                else if ((bytes[i] & 0xC0) != 0x80) col++;
            }
            return new SourceSpan(file, line, col);
        }

        StylesheetException Fail(long offset, string message) => new($"{Here(offset)}: {message}");

        public Stylesheet ReadSheet()
        {
            Next(); Expect(JsonTokenType.StartObject);
            while (Next() && _r.TokenType == JsonTokenType.PropertyName)
            {
                var key = _r.GetString()!;
                Next();
                switch (key)
                {
                    case "tokens": ReadTokens(_tokens); break;
                    case "rules": ReadRules(); break;
                    default: throw Fail(_r.TokenStartIndex, $"Unknown top-level key '{key}'.");
                }
            }
            return new Stylesheet(_tokens, _rules, Encoding.UTF8.GetString(bytes));
        }

        bool Next() => _r.Read();

        void Expect(JsonTokenType t)
        {
            if (_r.TokenType != t) throw Fail(_r.TokenStartIndex, $"Expected {t}, found {_r.TokenType}.");
        }

        void ReadTokens(Dictionary<string, TokenDeclaration> into)
        {
            Expect(JsonTokenType.StartObject);
            while (Next() && _r.TokenType == JsonTokenType.PropertyName)
            {
                var name = _r.GetString()!; var at = _r.TokenStartIndex;
                Next();
                into[name] = new TokenDeclaration(name, ReadScalar(), Here(at));
            }
        }

        string ReadScalar()
        {
            switch (_r.TokenType)
            {
                case JsonTokenType.String: return _r.GetString()!;
                case JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False:
                    return Encoding.UTF8.GetString(_r.ValueSpan);
                default: throw Fail(_r.TokenStartIndex, $"Expected a value, found {_r.TokenType}.");
            }
        }

        void ReadRules()
        {
            Expect(JsonTokenType.StartObject);
            while (Next() && _r.TokenType == JsonTokenType.PropertyName)
            {
                var selectorText = _r.GetString()!; var at = _r.TokenStartIndex;
                Next();
                ReadRule(selectorText, at);
            }
        }

        void ReadRule(string selectorText, long at)
        {
            Selector selector;
            try { selector = Selector.Parse(selectorText); }
            catch (FormatException ex) { throw Fail(at, ex.Message); }

            Expect(JsonTokenType.StartObject);
            var slot = _rules.Count;
            _rules.Add(null!); // reserve so the parent precedes its nested Rules
            var decls = new List<Declaration>();
            var tokens = new Dictionary<string, TokenDeclaration>(StringComparer.Ordinal);

            while (Next() && _r.TokenType == JsonTokenType.PropertyName)
            {
                var key = _r.GetString()!; var keyAt = _r.TokenStartIndex;
                Next();
                if (_r.TokenType == JsonTokenType.StartObject)
                {
                    var nested = key.Contains('&') ? key.Replace("&", selectorText) : selectorText + " " + key;
                    ReadRule(nested, keyAt);
                }
                else if (key.StartsWith("--", StringComparison.Ordinal))
                    tokens[key] = new TokenDeclaration(key, ReadScalar(), Here(keyAt));
                else
                    decls.Add(new Declaration(key, ReadScalar(), Here(keyAt)));
            }
            _rules[slot] = new Rule(selector, decls, tokens, Here(at));
        }
    }
}
