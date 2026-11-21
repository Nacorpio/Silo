using System.Text;

namespace Silo.Styling;

/// <summary>
/// The Declared value that won the Cascade for one Styled property, together with the Declaration it came from.
/// </summary>
/// <param name="Property">The Styled property name in kebab-case.</param>
/// <param name="Value">The winning value text, not yet resolved against Tokens.</param>
/// <param name="Source">
/// The winning Declaration, including its <see cref="SourceSpan"/>. This is what the Inspector's Cascade trace shows.
/// </param>
public sealed record CascadedValue(string Property, string Value, Declaration Source);

/// <summary>
/// Resolves Tokens and Cascaded values for an Element from an ordered list of Stylesheets.
/// </summary>
/// <param name="sheets">
/// The Stylesheets in load order, lowest priority first: the Default theme, then the app's Stylesheets. A Rule in a later
/// Stylesheet beats an equally specific Rule in an earlier one. The list is read on every call, not copied.
/// </param>
/// <remarks>
/// Higher specificity beats source order, and for equal specificity the later Rule wins. The resolver covers the
/// Stylesheet part of the Cascade only, and does not look at inherited values, Cascade layers or Local values.
/// </remarks>
/// <example>
/// Resolve the background of a hovered primary Button:
/// <code language="csharp">
/// var theme = JsoncStylesheetReader.Read("""
///     {
///       "tokens": { "--accent": "#3b82f6" },
///       "rules": {
///         "Button": { "background": "gray", "&amp;:hover": { "background": "var(--accent)" } },
///         ".danger": { "--accent": "#dc2626" },
///       }
///     }
///     """, "theme.jsonc");
///
/// var resolver = new StyleResolver([theme]);
///
/// var cascaded = resolver.Cascade(hoveredButton);                // hoveredButton is an IStyleNode in the :hover state
/// var tokens = resolver.ResolveTokens(hoveredButton);
/// var background = StyleResolver.Compute(cascaded["background"].Value, tokens);   // "#3b82f6"
/// </code>
/// </example>
public sealed class StyleResolver(IReadOnlyList<Stylesheet> sheets)
{
    /// <summary>The deepest <c>var()</c> nesting that <see cref="Compute"/> follows before it assumes the Tokens form a cycle.</summary>
    const int MaxTokenDepth = 32;

    /// <summary>
    /// Resolves the Cascaded value of every Styled property that a Rule declares for an Element, or for one of its Parts.
    /// </summary>
    /// <param name="node">The Element to resolve values for. Its type, name, Classes and Pseudo-states decide which Selectors match.</param>
    /// <param name="part">
    /// The name of a Part of <paramref name="node"/> to resolve instead of the Element itself, as written in <c>::part(name)</c>.
    /// Pass <see langword="null"/> (the default) for the Element. Rules for a Part match against the owning Element, so
    /// <paramref name="node"/> is always the Control that owns the Part.
    /// </param>
    /// <returns>
    /// A dictionary keyed by Styled property name (kebab-case, compared ordinally). It holds only the properties that at least
    /// one matching Rule declares, so it is empty when nothing matches. Each <see cref="CascadedValue.Value"/> is the Declared
    /// text exactly as written, so <c>var()</c> references are still unresolved; pass it to <see cref="Compute"/> together with
    /// the result of <see cref="ResolveTokens"/> to get the Computed text. Token declarations (names starting with <c>--</c>)
    /// are not included here.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Matching Rules are ordered from lowest to highest priority and applied in that order, so a later Declaration replaces an
    /// earlier one for the same property. Priority is the Selector's specificity first (ids, then classes and Pseudo-states,
    /// then types), and for equal specificity the source order, where a Rule in a later Stylesheet beats any Rule in an earlier
    /// one and a later Rule in the same Stylesheet beats an earlier one.
    /// </para>
    /// <para>
    /// Only Stylesheet Rules take part. Inheritance, Cascade layers and Local values set in C# are applied by the caller.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var resolver = new StyleResolver([sheet]);
    ///
    /// var element = resolver.Cascade(button);                 // values for the Button itself
    /// var icon = resolver.Cascade(button, "icon");            // values from ::part(icon) rules
    ///
    /// element["background"].Source.Span;                      // where the winning value was written
    /// </code>
    /// </example>
    public IReadOnlyDictionary<string, CascadedValue> Cascade(IStyleNode node, string? part = null)
    {
        var result = new Dictionary<string, CascadedValue>(StringComparer.Ordinal);

        foreach (var rule in MatchingRules(node, part))
        {
            foreach (var declaration in rule.Declarations)
            {
                result[declaration.Property] = new CascadedValue(declaration.Property, declaration.Value, declaration);
            }
        }

        return result;
    }

    /// <summary>
    /// Collects the Tokens that are visible at an Element, following the rule that Tokens inherit down the Element tree.
    /// </summary>
    /// <param name="node">The Element whose visible Tokens are wanted. Its ancestors are read through <see cref="IStyleNode.Parent"/>.</param>
    /// <returns>
    /// A dictionary from Token name, including the leading <c>--</c>, to the Declared text of its value. Values are not
    /// resolved, so a Token may itself contain <c>var()</c> references; <see cref="Compute"/> resolves them. The dictionary
    /// is new on every call and the caller may change it.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Resolution starts with the top-level Tokens of every Stylesheet, where a later Stylesheet overrides an earlier one.
    /// It then walks from the root Element down to <paramref name="node"/>. At each Element, the Tokens declared by the
    /// matching Rules are applied in Cascade order (lowest priority first), so a Token set closer to
    /// <paramref name="node"/> overrides one set on an ancestor, and a more specific Rule overrides a less specific one on the
    /// same Element. Rules that target a Part with <c>::part()</c> do not contribute.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, string> ResolveTokens(IStyleNode node)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var sheet in sheets)
        {
            foreach (var token in sheet.Tokens.Values)
            {
                tokens[token.Name] = token.Value;
            }
        }

        var chain = new Stack<IStyleNode>();

        for (var current = node; current is not null; current = current.Parent)
        {
            chain.Push(current);
        }

        foreach (var element in chain)
        {
            foreach (var rule in MatchingRules(element, part: null))
            {
                foreach (var token in rule.Tokens.Values)
                {
                    tokens[token.Name] = token.Value;
                }
            }
        }

        return tokens;
    }

    /// <summary>
    /// Replaces every <c>var(--name)</c> and <c>var(--name, fallback)</c> in a value with the text of the named Token, which
    /// turns a Cascaded value into a Computed one as far as Tokens are concerned.
    /// </summary>
    /// <param name="value">The Declared value text, for example <c>"1px solid var(--border, #333)"</c>.</param>
    /// <param name="tokens">
    /// The Tokens to look names up in, usually the result of <see cref="ResolveTokens"/>. Keys include the leading
    /// <c>--</c>.
    /// </param>
    /// <returns>
    /// The value with all references replaced. A value without <c>var(</c> is returned unchanged. The result is not parsed or
    /// validated, so it is still text that a value parser has to read.
    /// </returns>
    /// <exception cref="StylesheetException">
    /// A referenced Token is not in <paramref name="tokens"/> and the reference has no fallback, a <c>var(</c> has no closing
    /// parenthesis, or Tokens refer to each other in a cycle (detected as nesting deeper than 32 levels).
    /// </exception>
    /// <remarks>
    /// The text before the first comma inside <c>var()</c> is the Token name and the text after it, trimmed, is the fallback.
    /// A Token's own value and a fallback are both resolved recursively, so Tokens may refer to other Tokens and a fallback may
    /// contain further <c>var()</c> references. A fallback is used only when the Token is missing; a Token that exists is used
    /// even if its value is empty.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var tokens = new Dictionary&lt;string, string&gt; { ["--gap"] = "4px" };
    ///
    /// StyleResolver.Compute("var(--gap) var(--missing, 8px)", tokens);   // "4px 8px"
    /// StyleResolver.Compute("var(--missing)", tokens);                   // throws StylesheetException
    /// </code>
    /// </example>
    public static string Compute(string value, IReadOnlyDictionary<string, string> tokens)
        => Substitute(value, tokens, 0);

    /// <summary>Finds the Rules that match an Element or Part, ordered from lowest to highest priority: specificity, then source order.</summary>
    /// <param name="node">The Element to match against.</param>
    /// <param name="part">The Part name, or <see langword="null"/> for the Element itself.</param>
    IEnumerable<Rule> MatchingRules(IStyleNode node, string? part)
    {
        var matches = new List<(Rule Rule, int Order)>();
        var order = 0;

        foreach (var sheet in sheets)
        {
            foreach (var rule in sheet.Rules)
            {
                if (rule.Selector.Part == part && rule.Selector.Matches(node))
                {
                    matches.Add((rule, order));
                }

                order++;
            }
        }

        return matches
            .OrderBy(m => m.Rule.Selector.Specificity)
            .ThenBy(m => m.Order)
            .Select(m => m.Rule);
    }

    /// <summary>Replaces <c>var()</c> references in <paramref name="value"/>, recursing into Token values and fallbacks. <paramref name="depth"/> counts the nesting.</summary>
    static string Substitute(string value, IReadOnlyDictionary<string, string> tokens, int depth)
    {
        if (depth > MaxTokenDepth)
        {
            throw new StylesheetException($"Token references are nested too deeply (cycle?) in '{value}'.");
        }

        var start = value.IndexOf("var(", StringComparison.Ordinal);

        if (start < 0)
        {
            return value;
        }

        var sb = new StringBuilder();
        var position = 0;

        while (start >= 0)
        {
            sb.Append(value, position, start - position);

            var end = FindClosingParenthesis(value, start + 3);

            var inner = value[(start + 4)..end];
            var comma = inner.IndexOf(',');

            var name = (comma < 0 ? inner : inner[..comma]).Trim();
            var fallback = comma < 0 ? null : inner[(comma + 1)..].Trim();

            if (tokens.TryGetValue(name, out var token))
            {
                sb.Append(Substitute(token, tokens, depth + 1));
            }
            else if (fallback is not null)
            {
                sb.Append(Substitute(fallback, tokens, depth + 1));
            }
            else
            {
                throw new StylesheetException($"Undefined Token '{name}'.");
            }

            position = end + 1;
            start = value.IndexOf("var(", position, StringComparison.Ordinal);
        }

        sb.Append(value, position, value.Length - position);

        return sb.ToString();
    }

    /// <summary>Returns the index of the parenthesis that closes the one at <paramref name="open"/>, counting nested pairs.</summary>
    static int FindClosingParenthesis(string value, int open)
    {
        var depth = 0;

        for (var i = open; i < value.Length; i++)
        {
            if (value[i] == '(')
            {
                depth++;
            }
            else if (value[i] == ')' && --depth == 0)
            {
                return i;
            }
        }

        throw new StylesheetException($"Unclosed var( in '{value}'.");
    }
}
