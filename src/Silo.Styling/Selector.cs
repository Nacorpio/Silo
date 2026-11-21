using System.Text;

namespace Silo.Styling;

/// <summary>
/// How specific a <see cref="Selector"/> is, as a triple that is compared like CSS specificity: ids first, then classes, then
/// types.
/// </summary>
/// <param name="Ids">The number of <c>#name</c> parts.</param>
/// <param name="Classes">The number of <c>.class</c> parts plus the number of Pseudo-states such as <c>:hover</c>.</param>
/// <param name="Types">The number of type names, plus one for a <c>::part()</c>. The universal <c>*</c> adds nothing.</param>
/// <remarks>
/// A larger value in an earlier position always wins: <c>#save</c> beats any number of classes, and one class beats any
/// number of types. Use <see cref="CompareTo"/> or the comparison on <see cref="IComparable{T}"/> to order Selectors.
/// </remarks>
/// <example>
/// <code language="csharp">
/// Selector.Parse("Button.primary:hover").Specificity;  // Specificity(Ids: 0, Classes: 2, Types: 1)
/// Selector.Parse("#save").Specificity;                 // Specificity(Ids: 1, Classes: 0, Types: 0)
///
/// Selector.Parse("#save").Specificity.CompareTo(Selector.Parse(".a.b.c").Specificity);  // greater than 0
/// </code>
/// </example>
public readonly record struct Specificity(int Ids, int Classes, int Types) : IComparable<Specificity>
{
    /// <summary>Compares this specificity with another, ids first, then classes, then types.</summary>
    /// <param name="other">The specificity to compare with.</param>
    /// <returns>
    /// A negative number if this one is less specific, zero if they are equal, and a positive number if this one is more
    /// specific.
    /// </returns>
    public int CompareTo(Specificity other)
    {
        var c = Ids.CompareTo(other.Ids);
        if (c != 0) return c;
        c = Classes.CompareTo(other.Classes);
        return c != 0 ? c : Types.CompareTo(other.Types);
    }
}

/// <summary>How two compound Selectors are joined.</summary>
public enum Combinator
{
    /// <summary>No combinator. Only the first compound of a Selector has this, because nothing comes before it.</summary>
    None,

    /// <summary>White space, as in <c>.card Button</c>: the right side matches any descendant of an Element matching the left side.</summary>
    Descendant,

    /// <summary><c>&gt;</c>, as in <c>Box &gt; Button</c>: the right side matches only a direct child of an Element matching the left side.</summary>
    Child,
}

/// <summary>
/// One part of a <see cref="Selector"/> between combinators, such as <c>Button.primary:hover</c>. It tests a single Element
/// and knows nothing about the Element's ancestors.
/// </summary>
/// <param name="Type">The type name, <c>*</c> for any type, or <see langword="null"/> when the compound has no type.</param>
/// <param name="Id">The name that the Element must have, from <c>#name</c>, or <see langword="null"/>.</param>
/// <param name="Classes">The Classes the Element must all have, from <c>.class</c> parts.</param>
/// <param name="States">The Pseudo-states the Element must all be in, from <c>:state</c> parts, without the colon.</param>
/// <param name="Part">The Part named by <c>::part(name)</c>, or <see langword="null"/>. It is only allowed on the last compound.</param>
public sealed record CompoundSelector(
    string? Type, string? Id, IReadOnlyList<string> Classes, IReadOnlyList<string> States, string? Part)
{
    /// <summary>Tests whether an Element satisfies the type, name, Classes and Pseudo-states of this compound.</summary>
    /// <param name="node">The Element to test.</param>
    /// <returns>
    /// <see langword="true"/> if every part matches. <see cref="Part"/> is not tested, because it names something inside the
    /// matching Element and not the Element itself.
    /// </returns>
    public bool Matches(IStyleNode node)
    {
        if (Type is not null && Type != "*" && node.TypeName != Type) return false;
        if (Id is not null && node.Name != Id) return false;
        foreach (var c in Classes) if (!node.Classes.Contains(c)) return false;
        foreach (var s in States) if (!node.PseudoStates.Contains(s)) return false;
        return true;
    }

    /// <summary>Writes the compound back in Selector syntax.</summary>
    /// <returns>For example <c>Button.primary:hover::part(icon)</c>.</returns>
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

/// <summary>
/// The pattern in a Rule that decides which Elements it applies to: one or more compound Selectors joined by descendant or
/// child combinators, optionally ending in a Part. Create one with <see cref="Parse"/>.
/// </summary>
/// <remarks>
/// <para>
/// The supported syntax is a type name or <c>*</c>, <c>.class</c>, <c>#name</c>, Pseudo-states such as <c>:hover</c>,
/// <c>::part(name)</c> on the last compound, white space for the descendant combinator and <c>&gt;</c> for the child
/// combinator. A compound may combine these, as in <c>Button.primary:hover</c>. Attribute Selectors, <c>+</c> and <c>~</c>,
/// and selector lists are not supported.
/// </para>
/// <para>
/// A Selector is immutable and safe to share between threads.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var selector = Selector.Parse("Box &gt; Button.primary:hover");
///
/// selector.Matches(button);                 // true when button is a hovered .primary Button directly inside a Box
/// selector.Specificity;                     // Specificity(Ids: 0, Classes: 2, Types: 2)
///
/// Selector.Parse("Button::part(icon)").Part;  // "icon"
/// </code>
/// </example>
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

    /// <summary>The specificity of the whole Selector, added up over all of its compounds.</summary>
    public Specificity Specificity { get; }

    /// <summary>
    /// The name of the Part that the Selector targets, from a trailing <c>::part(name)</c>, or <see langword="null"/> when it
    /// targets the Element itself.
    /// </summary>
    public string? Part => _compounds[^1].Part;

    /// <summary>
    /// Tests whether the Selector matches an Element, looking at the Element and, for combinators, at its ancestors.
    /// </summary>
    /// <param name="node">The Element to test. For a <c>::part()</c> Selector this is the Control that owns the Part.</param>
    /// <returns>
    /// <see langword="true"/> if the rightmost compound matches <paramref name="node"/> and every compound to its left is
    /// matched by a fitting ancestor. A descendant combinator accepts any ancestor, and a child combinator only the parent.
    /// </returns>
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

    /// <summary>Writes the Selector in a normalized form.</summary>
    /// <returns>
    /// The Selector with single spaces around combinators, for example <c>Box &gt; Button.primary</c>. Parsing the result gives
    /// an equivalent Selector.
    /// </returns>
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

    /// <summary>Parses Selector text.</summary>
    /// <param name="text">The Selector, for example <c>Button.primary:hover</c> or <c>.card &gt; Box</c>.</param>
    /// <returns>The parsed Selector.</returns>
    /// <exception cref="FormatException">
    /// The text is empty, has a stray or trailing <c>&gt;</c>, has an empty or malformed name, uses an unsupported
    /// pseudo-element, or puts <c>::part()</c> anywhere but the last compound. The message names the problem.
    /// </exception>
    /// <example>
    /// <code language="csharp">
    /// var ok = Selector.Parse("Box&gt;Button");   // same as "Box &gt; Button"
    ///
    /// Selector.Parse("Button..x");               // throws FormatException
    /// </code>
    /// </example>
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
