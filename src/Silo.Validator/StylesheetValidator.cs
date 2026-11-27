using System.Text.RegularExpressions;
using Silo.Styling;

namespace Silo.Validation;

/// <summary>
/// Checks a Stylesheet against the Controls without opening a window: property names, values, Tokens, type names and
/// Pseudo-states.
/// </summary>
/// <remarks>
/// <para>
/// The checks are the ones that applying the Stylesheet to a Control would report at run time, found ahead of time:
/// </para>
/// <list type="bullet">
/// <item><description><c>SILO000</c> (error): the text is not a valid Stylesheet.</description></item>
/// <item><description><c>SILO001</c> (error): the type has no Styled property with that name.</description></item>
/// <item><description><c>SILO002</c> (error): the property does not accept the value.</description></item>
/// <item><description><c>SILO003</c> (error): a <c>var()</c> names a Token that nothing declares, or Tokens refer to each other in a cycle.</description></item>
/// <item><description><c>SILO004</c> (warning): no Control has the type name; it may be one of the application's own.</description></item>
/// <item><description><c>SILO005</c> (warning): no Control sets the Pseudo-state.</description></item>
/// </list>
/// <para>
/// A Token that a Rule declares can have a different value on each Element, so a value that uses one is not checked
/// against its property, only for the Token being declared somewhere.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var issues = StylesheetValidator.ValidateText(File.ReadAllText("theme.jsonc"), "theme.jsonc");
///
/// foreach (var issue in issues)
///     Console.WriteLine(issue);   // theme.jsonc(4,9): error SILO001: Button has no Styled property 'colour'.
/// </code>
/// </example>
public static partial class StylesheetValidator
{
    /// <summary>The Pseudo-states that the Controls declare with <see cref="PseudoStatesAttribute"/>, in order.</summary>
    static string[] States => [.. Types.Value.Values
        .SelectMany(t => Chain(t))
        .SelectMany(t => t.GetCustomAttributes(typeof(PseudoStatesAttribute), inherit: false).Cast<PseudoStatesAttribute>())
        .SelectMany(a => a.States)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)];

    /// <summary>The Control types by the name that Selectors use.</summary>
    static readonly Lazy<Dictionary<string, Type>> Types = new(() => FindTypes(typeof(Control).Assembly));

    /// <summary>Finds the <c>var(--name</c> references in a value.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"var\(\s*(--[^\s,)]+)")]
    private static partial Regex References();

    /// <summary>Adds the Controls of another assembly, such as an add-on library, to the types that Selectors may name.</summary>
    /// <param name="assembly">The assembly. Its public Control types are found by name and their Styled properties are set up.</param>
    /// <remarks>Call it before validating. A type name that Silo.Controls already has is kept.</remarks>
    /// <example>
    /// <code language="csharp">
    /// StylesheetValidator.AddControls(typeof(QuantityBox).Assembly);
    /// </code>
    /// </example>
    public static void AddControls(System.Reflection.Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var types = Types.Value;

        foreach (var (name, type) in FindTypes(assembly))
            types.TryAdd(name, type);
    }

    /// <summary>Lists a type and its base types up to <see cref="Control"/>, which includes abstract ones such as ToggleButton.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The types, most derived first.</returns>
    static IEnumerable<Type> Chain(Type type)
    {
        for (var t = type; t is not null && typeof(Control).IsAssignableFrom(t); t = t.BaseType)
            yield return t;
    }

    /// <summary>Finds the Control types of an assembly and sets up their Styled properties.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The types by name.</returns>
    static Dictionary<string, Type> FindTypes(System.Reflection.Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => typeof(Control).IsAssignableFrom(t) && t.IsPublic && !t.IsAbstract)
            .ToDictionary(t => t.Name, StringComparer.Ordinal);

        foreach (var type in assembly.GetTypes().Where(t => typeof(Control).IsAssignableFrom(t)))
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

        types["Control"] = typeof(Control);

        return types;
    }

    /// <summary>Reads and validates Stylesheet text.</summary>
    /// <param name="json">The JSONC text.</param>
    /// <param name="fileName">The name to report in positions.</param>
    /// <param name="context">
    /// Stylesheets that the text is used together with, such as the default theme. Their Tokens count as declared.
    /// </param>
    /// <returns>The issues in source order. A text that cannot be read gives a single <c>SILO000</c> error.</returns>
    public static IReadOnlyList<ValidationIssue> ValidateText(
        string json, string fileName, IReadOnlyList<Stylesheet>? context = null)
    {
        try
        {
            return Validate(JsoncStylesheetReader.Read(json, fileName), context);
        }
        catch (StylesheetException ex)
        {
            return [SyntaxIssue(ex.Message, fileName)];
        }
    }

    /// <summary>Turns the message of a reader error into an issue.</summary>
    /// <param name="message">The message, which starts with <c>file(line,column): </c>.</param>
    /// <param name="fileName">The file name, used when the message has no position.</param>
    /// <returns>The <c>SILO000</c> issue.</returns>
    static ValidationIssue SyntaxIssue(string message, string fileName)
    {
        var pattern = @"^(?<file>.*)\((?<line>\d+),(?<column>\d+)\): (?<text>.*)$";
        var match = Regex.Match(message, pattern, RegexOptions.Singleline);

        if (!match.Success)
            return new(ValidationSeverity.Error, "SILO000", message, new(fileName, 1, 1));

        var line = int.Parse(match.Groups["line"].Value);
        var column = int.Parse(match.Groups["column"].Value);
        var span = new SourceSpan(match.Groups["file"].Value, line, column);

        return new(ValidationSeverity.Error, "SILO000", match.Groups["text"].Value, span);
    }

    /// <summary>Validates a Stylesheet that has been read.</summary>
    /// <param name="sheet">The Stylesheet.</param>
    /// <param name="context">Stylesheets that it is used together with. Only <paramref name="sheet"/> is checked.</param>
    /// <returns>The issues in source order.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(Stylesheet sheet, IReadOnlyList<Stylesheet>? context = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var all = new List<Stylesheet>(context ?? []) { sheet };
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var ruleTokens = new HashSet<string>(StringComparer.Ordinal);
        var issues = new List<ValidationIssue>();

        foreach (var other in all)
        {
            foreach (var token in other.Tokens.Values)
                tokens[token.Name] = token.Value;

            foreach (var token in other.Rules.SelectMany(r => r.Tokens.Values))
            {
                tokens[token.Name] = token.Value;
                ruleTokens.Add(token.Name);
            }
        }

        foreach (var rule in sheet.Rules)
        {
            CheckSelector(rule, issues);

            foreach (var declaration in rule.Declarations)
                CheckDeclaration(rule, declaration, tokens, ruleTokens, issues);

            foreach (var token in rule.Tokens.Values)
                CheckValue(token.Value, token.Span, tokens, issues);
        }

        foreach (var token in sheet.Tokens.Values)
            CheckValue(token.Value, token.Span, tokens, issues);

        return [.. issues.OrderBy(i => i.Span.Line).ThenBy(i => i.Span.Column)];
    }

    /// <summary>Checks the type names and Pseudo-states of a Rule's Selector.</summary>
    /// <param name="rule">The Rule.</param>
    /// <param name="issues">Where to add the issues.</param>
    static void CheckSelector(Rule rule, List<ValidationIssue> issues)
    {
        foreach (var compound in rule.Selector.Compounds)
        {
            if (compound.Type is not null and not "*" && !Types.Value.ContainsKey(compound.Type))
            {
                var message = $"No Control is named '{compound.Type}'. Ignore this if it is one of your own.";

                issues.Add(new(ValidationSeverity.Warning, "SILO004", message, rule.Span));
            }

            var declared = States;

            foreach (var state in compound.States.Where(s => !declared.Contains(s)))
            {
                var known = string.Join(", ", declared);
                var message = $"No Control sets the Pseudo-state ':{state}'. The known ones are {known}.";

                issues.Add(new(ValidationSeverity.Warning, "SILO005", message, rule.Span));
            }
        }
    }

    /// <summary>Checks the property name and value of a declaration.</summary>
    /// <param name="rule">The Rule that holds it.</param>
    /// <param name="declaration">The declaration.</param>
    /// <param name="tokens">Every Token that is declared, by name.</param>
    /// <param name="ruleTokens">The names of the Tokens that a Rule declares.</param>
    /// <param name="issues">Where to add the issues.</param>
    static void CheckDeclaration(
        Rule rule,
        Declaration declaration,
        Dictionary<string, string> tokens,
        HashSet<string> ruleTokens,
        List<ValidationIssue> issues)
    {
        var property = FindProperty(rule.Selector, declaration.Property, out var known);

        if (property is null && known)
        {
            var type = rule.Selector.Compounds[^1].Type!;

            var message = $"{type} has no Styled property '{declaration.Property}'.";

            issues.Add(new(ValidationSeverity.Error, "SILO001", message, declaration.Span));

            return;
        }

        if (!CheckValue(declaration.Value, declaration.Span, tokens, issues))
            return;

        var dynamic = References().Matches(declaration.Value).Any(m => ruleTokens.Contains(m.Groups[1].Value));

        if (property is null || dynamic)
            return;

        var computed = StyleResolver.Compute(declaration.Value, tokens);

        if (!property.Accepts(computed))
        {
            var message = $"'{computed}' is not a valid value for '{declaration.Property}'.";

            issues.Add(new(ValidationSeverity.Error, "SILO002", message, declaration.Span));
        }
    }

    /// <summary>Finds the Styled property that a declaration sets.</summary>
    /// <param name="selector">The Selector of the Rule, whose last compound decides the Control type.</param>
    /// <param name="name">The property name.</param>
    /// <param name="known">
    /// Whether the property can be told to be missing: the Selector names a Control type and does not target a Part.
    /// </param>
    /// <returns>The property, or <see langword="null"/>.</returns>
    static StyledProperty? FindProperty(Selector selector, string name, out bool known)
    {
        var last = selector.Compounds[^1];

        known = last.Part is null && last.Type is not null && Types.Value.ContainsKey(last.Type);

        if (known)
            return StyledProperty.Find(Types.Value[last.Type!], name);

        return Types.Value.Values.Select(t => StyledProperty.Find(t, name)).FirstOrDefault(p => p is not null);
    }

    /// <summary>Checks that the Tokens that a value refers to exist and do not form a cycle.</summary>
    /// <param name="value">The Declared text.</param>
    /// <param name="span">Where it is declared.</param>
    /// <param name="tokens">Every Token that is declared, by name.</param>
    /// <param name="issues">Where to add the issues.</param>
    /// <returns><see langword="true"/> when the value can be computed.</returns>
    static bool CheckValue(
        string value, SourceSpan span, Dictionary<string, string> tokens, List<ValidationIssue> issues)
    {
        try
        {
            StyleResolver.Compute(value, tokens);

            return true;
        }
        catch (StylesheetException ex)
        {
            issues.Add(new(ValidationSeverity.Error, "SILO003", ex.Message, span));

            return false;
        }
    }
}
