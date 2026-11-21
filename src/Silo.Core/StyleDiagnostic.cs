using Silo.Styling;

namespace Silo;

/// <summary>
/// A problem found while applying Stylesheets to an Element, such as a value that does not parse.
/// </summary>
/// <param name="Element">The Element the Declaration was applied to.</param>
/// <param name="Property">The Styled property name in kebab-case, as written in the Stylesheet.</param>
/// <param name="Value">The Declared text of the value.</param>
/// <param name="Span">Where the Declaration was written, for messages and the Inspector.</param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <remarks>
/// Invalid Declarations are skipped and do not stop the rest of the Stylesheet from applying, so a typo never leaves an
/// Element unstyled. The caller decides whether to log the diagnostics or fail.
/// </remarks>
public sealed record StyleDiagnostic(Element Element, string Property, string Value, SourceSpan Span, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Span.File}({Span.Line},{Span.Column}): {Property}: {Message}";
}
