using System.Text;

namespace Silo.Styling;

/// <summary>The Declared value that won the Cascade for one Styled property, with where it came from.</summary>
public sealed record CascadedValue(string Property, string Value, Declaration Source);

/// <summary>
/// Resolves Tokens and Cascaded values for an Element from an ordered list of Stylesheets.
/// Later Stylesheets and later Rules win ties; higher specificity beats source order.
/// </summary>
public sealed class StyleResolver(IReadOnlyList<Stylesheet> sheets)
{
    const int MaxTokenDepth = 32;

    /// <summary>Resolves the Cascaded value of every Styled property declared for the Element, or for one of its Parts.</summary>
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

    /// <summary>Collects the Tokens visible at the Element: Stylesheet Tokens, then Rule Tokens from the root down to the Element.</summary>
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

    /// <summary>Replaces <c>var(--name)</c> and <c>var(--name, fallback)</c> using the given Tokens.</summary>
    public static string Compute(string value, IReadOnlyDictionary<string, string> tokens)
        => Substitute(value, tokens, 0);

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
