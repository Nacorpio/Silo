using System.Text;

namespace Silo.Styling;

public readonly record struct Specificity(int Ids, int Classes, int Types) : IComparable<Specificity>
{
    public int CompareTo(Specificity other)
    {
        var c = Ids.CompareTo(other.Ids);
        if (c != 0) return c;
        c = Classes.CompareTo(other.Classes);
        return c != 0 ? c : Types.CompareTo(other.Types);
    }
}

public enum Combinator { None, Descendant, Child }

public sealed record CompoundSelector(
    string? Type, string? Id, IReadOnlyList<string> Classes, IReadOnlyList<string> States, string? Part)
{
    public bool Matches(IStyleNode node)
    {
        if (Type is not null && Type != "*" && node.TypeName != Type) return false;
        if (Id is not null && node.Name != Id) return false;
        foreach (var c in Classes) if (!node.Classes.Contains(c)) return false;
        foreach (var s in States) if (!node.PseudoStates.Contains(s)) return false;
        return true;
    }

    public override string ToString()
    {
        var sb = new StringBuilder(Type);
        if (Id is not null) sb.Append('#').Append(Id);
        foreach (var c in Classes) sb.Append('.').Append(c);
        foreach (var s in States) sb.Append(':').Append(s);
        if (Part is not null) sb.Append("::part(").Append(Part).Append(')');
        return sb.ToString();
    }
}

/// <summary>A parsed Selector: compounds joined by combinators. Combinator[i] sits between compound i-1 and i.</summary>
public sealed class Selector
{
    readonly CompoundSelector[] _compounds;
    readonly Combinator[] _combinators;

    Selector(CompoundSelector[] compounds, Combinator[] combinators)
    {
        _compounds = compounds;
        _combinators = combinators;
        var ids = 0; var classes = 0; var types = 0;
        foreach (var c in compounds)
        {
            if (c.Id is not null) ids++;
            classes += c.Classes.Count + c.States.Count;
            if (c.Type is not null && c.Type != "*") types++;
            if (c.Part is not null) types++; // a Part counts like a pseudo-element
        }
        Specificity = new Specificity(ids, classes, types);
    }

    public Specificity Specificity { get; }

    /// <summary>The Part targeted by the rightmost compound, or null when the Selector targets the Element itself.</summary>
    public string? Part => _compounds[^1].Part;

    /// <summary>True when the Element (the owner, for a ::part() Selector) matches.</summary>
    public bool Matches(IStyleNode node) => MatchFrom(_compounds.Length - 1, node);

    bool MatchFrom(int index, IStyleNode node)
    {
        if (!_compounds[index].Matches(node)) return false;
        if (index == 0) return true;

        switch (_combinators[index])
        {
            case Combinator.Child:
                return node.Parent is { } p && MatchFrom(index - 1, p);
            default:
                for (var a = node.Parent; a is not null; a = a.Parent)
                    if (MatchFrom(index - 1, a)) return true;

                return false;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _compounds.Length; i++)
        {
            if (i > 0) sb.Append(_combinators[i] == Combinator.Child ? " > " : " ");

            sb.Append(_compounds[i]);
        }
        return sb.ToString();
    }

    public static Selector Parse(string text)
    {
        var compounds = new List<CompoundSelector>();
        var combinators = new List<Combinator> { Combinator.None };
        var pos = 0;
        var pending = Combinator.None;

        while (true)
        {
            var sawSpace = false;

            while (pos < text.Length && char.IsWhiteSpace(text[pos])) { pos++; sawSpace = true; }
            if (pos >= text.Length) break;
            if (text[pos] == '>')
            {
                if (compounds.Count == 0 || pending == Combinator.Child) throw Bad(text, "unexpected '>'");
                pending = Combinator.Child; pos++; continue;
            }

            if (compounds.Count > 0)
            {
                if (pending == Combinator.None) pending = sawSpace ? Combinator.Descendant : Combinator.None;
                if (pending == Combinator.None) throw Bad(text, "expected a combinator");
                combinators.Add(pending);
            }
            compounds.Add(ParseCompound(text, ref pos));
            pending = Combinator.None;
        }

        if (compounds.Count == 0) throw Bad(text, "empty selector");
        if (pending == Combinator.Child) throw Bad(text, "trailing '>'");

        for (var i = 0; i < compounds.Count - 1; i++)
            if (compounds[i].Part is not null) throw Bad(text, "::part() must be last");
        return new Selector(compounds.ToArray(), combinators.ToArray());
    }

    static CompoundSelector ParseCompound(string text, ref int pos)
    {
        string? type = null, id = null, part = null;
        var classes = new List<string>(); var states = new List<string>();
        var start = pos;

        if (pos < text.Length && (IsIdentStart(text[pos]) || text[pos] == '*'))
            type = text[pos] == '*' ? text[pos++].ToString() : ReadIdent(text, ref pos);

        while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '>')
        {
            var ch = text[pos];
            if (ch == '.') { pos++; classes.Add(ReadIdent(text, ref pos)); }
            else if (ch == '#') { pos++; id = id is null ? ReadIdent(text, ref pos) : throw Bad(text, "two ids"); }
            else if (ch == ':' && pos + 1 < text.Length && text[pos + 1] == ':')
            {
                pos += 2;
                if (ReadIdent(text, ref pos) != "part" || pos >= text.Length || text[pos] != '(') throw Bad(text, "only ::part() is supported");
                pos++;
                part = ReadIdent(text, ref pos);
                if (pos >= text.Length || text[pos] != ')') throw Bad(text, "expected ')'");
                pos++;
            }
            else if (ch == ':') { pos++; states.Add(ReadIdent(text, ref pos)); }
            else throw Bad(text, $"unexpected '{ch}'");
        }

        if (pos == start) throw Bad(text, "empty compound");

        return new CompoundSelector(type, id, classes, states, part);
    }

    static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    static string ReadIdent(string text, ref int pos)
    {
        var start = pos;
        if (pos >= text.Length || !(IsIdentStart(text[pos]) || text[pos] == '-')) throw Bad(text, "expected an identifier");
        while (pos < text.Length && IsIdentChar(text[pos])) pos++;

        return text[start..pos];
    }

    static FormatException Bad(string text, string why) => new($"Invalid selector '{text}': {why}.");
}
