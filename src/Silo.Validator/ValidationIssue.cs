using Silo.Styling;

namespace Silo.Validation;

/// <summary>How serious a <see cref="ValidationIssue"/> is.</summary>
public enum ValidationSeverity
{
    /// <summary>Something that may be intended, such as a type name that comes from the application's own Controls.</summary>
    Warning,

    /// <summary>Something that can never work: the Stylesheet would report a diagnostic or throw when it is applied.</summary>
    Error,
}

/// <summary>A problem that <see cref="StylesheetValidator"/> found in a Stylesheet.</summary>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Code">The stable code of the check, such as <c>SILO001</c>.</param>
/// <param name="Message">A sentence that names the problem.</param>
/// <param name="Span">Where in the source it was found.</param>
/// <remarks>
/// <see cref="ToString"/> writes the canonical compiler format <c>file(line,column): error SILO001: message</c>, which
/// MSBuild and IDEs turn into entries of the error list.
/// </remarks>
public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Message, SourceSpan Span)
{
    /// <summary>Writes the issue in the canonical compiler format.</summary>
    /// <returns>For example <c>theme.jsonc(4,9): error SILO001: Button has no Styled property 'colour'.</c></returns>
    public override string ToString() => $"{Span}: {Severity.ToString().ToLowerInvariant()} {Code}: {Message}";
}
