namespace Silo.Styling;

public readonly record struct SourceSpan(string File, int Line, int Column)
{
    public override string ToString() => $"{File}({Line},{Column})";
}

public sealed record TokenDeclaration(string Name, string Value, SourceSpan Span);

public sealed record Declaration(string Property, string Value, SourceSpan Span);

public sealed record Rule(
    Selector Selector,
    IReadOnlyList<Declaration> Declarations,
    IReadOnlyDictionary<string, TokenDeclaration> Tokens,
    SourceSpan Span);

/// <summary>A syntax-independent set of Tokens and Rules, in source order.</summary>
public sealed record Stylesheet(
    IReadOnlyDictionary<string, TokenDeclaration> Tokens,
    IReadOnlyList<Rule> Rules,
    string Source);

public sealed class StylesheetException(string message) : Exception(message);
