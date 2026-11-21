namespace Silo.Styling;

/// <summary>
/// A position in a source file, kept on every Declared value so that tools can say where a value came from.
/// </summary>
/// <param name="File">The file name or path that the Stylesheet was read from, exactly as it was passed to the reader.</param>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Column">The 1-based column, counted in characters and not bytes.</param>
/// <remarks>
/// Spans are recorded in every build, not only in Debug, because the Cascade trace in the Inspector and the Stylesheet
/// validator both report them. <see cref="ToString"/> gives the <c>file(line,column)</c> form that MSBuild and most editors
/// turn into a clickable link.
/// </remarks>
public readonly record struct SourceSpan(string File, int Line, int Column)
{
    /// <summary>Formats the span as <c>file(line,column)</c>, for example <c>theme.jsonc(12,5)</c>.</summary>
    /// <returns>The span in the <c>file(line,column)</c> form.</returns>
    public override string ToString() => $"{File}({Line},{Column})";
}

/// <summary>
/// A Token declared in a Stylesheet or in a Rule, such as <c>"--accent": "#3b82f6"</c>.
/// </summary>
/// <param name="Name">The Token name including the leading <c>--</c>, for example <c>--accent</c>.</param>
/// <param name="Value">
/// The value text as written. It is not resolved, so it may contain <c>var()</c> references to other Tokens.
/// </param>
/// <param name="Span">Where the Token is declared.</param>
public sealed record TokenDeclaration(string Name, string Value, SourceSpan Span);

/// <summary>
/// A Declared value: one Styled property set by one Rule, such as <c>"padding": "8 16"</c>.
/// </summary>
/// <param name="Property">The Styled property name in kebab-case, for example <c>border-radius</c>.</param>
/// <param name="Value">
/// The value text as written, for example <c>"1px solid #333"</c> or <c>"var(--radius)"</c>. It is parsed later by the typed
/// value parsers in <c>Silo.Styling.Values</c>, once Tokens have been resolved.
/// </param>
/// <param name="Span">Where the value is declared.</param>
public sealed record Declaration(string Property, string Value, SourceSpan Span);

/// <summary>
/// A Selector paired with the Styled property values and Tokens it sets.
/// </summary>
/// <param name="Selector">The Selector that decides which Elements, or which Part of an Element, the Rule applies to.</param>
/// <param name="Declarations">The Declared values in source order. Tokens are not in this list.</param>
/// <param name="Tokens">The Tokens the Rule declares, keyed by name including the leading <c>--</c>.</param>
/// <param name="Span">Where the Rule starts, which is the position of its Selector.</param>
/// <remarks>
/// Nested Rules are flattened while a Stylesheet is read. <c>"Button": { "&amp;:hover": { } }</c> becomes two Rules, with
/// the Selectors <c>Button</c> and <c>Button:hover</c>, so a <see cref="Rule"/> never contains other Rules.
/// </remarks>
public sealed record Rule(
    Selector Selector,
    IReadOnlyList<Declaration> Declarations,
    IReadOnlyDictionary<string, TokenDeclaration> Tokens,
    SourceSpan Span);

/// <summary>
/// A syntax-independent set of Tokens and Rules, in source order. The styling engine works on this model, and each
/// Stylesheet format, such as JSONC, is a reader that produces it.
/// </summary>
/// <param name="Tokens">The Tokens declared at the top level of the Stylesheet, keyed by name including the leading <c>--</c>.</param>
/// <param name="Rules">The Rules in source order, with nested Rules already flattened.</param>
/// <param name="Source">The complete source text that the Stylesheet was read from, kept so tools can show it.</param>
/// <example>
/// Read a Stylesheet and look at what it holds:
/// <code language="csharp">
/// var sheet = JsoncStylesheetReader.Read("""
///     {
///       "tokens": { "--accent": "#3b82f6" },
///       "rules": { "Button.primary": { "background": "var(--accent)" } }
///     }
///     """, "app.jsonc");
///
/// sheet.Tokens["--accent"].Value;            // "#3b82f6"
/// sheet.Rules[0].Selector.ToString();        // "Button.primary"
/// sheet.Rules[0].Declarations[0].Property;   // "background"
/// </code>
/// </example>
public sealed record Stylesheet(
    IReadOnlyDictionary<string, TokenDeclaration> Tokens,
    IReadOnlyList<Rule> Rules,
    string Source);

/// <summary>
/// The error that Silo.Styling throws when a Stylesheet is malformed or a Token cannot be resolved.
/// </summary>
/// <param name="message">
/// The description of the problem. Errors from a Stylesheet reader start with the position in the form
/// <c>file(line,column): </c>.
/// </param>
public sealed class StylesheetException(string message) : Exception(message);
