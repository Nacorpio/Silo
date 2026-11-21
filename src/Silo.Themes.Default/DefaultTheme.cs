using System.Reflection;
using Silo.Styling;

namespace Silo;

/// <summary>
/// The built-in default theme: a Stylesheet embedded in this assembly that styles <c>Box</c>, <c>TextBlock</c> and
/// <c>Button</c> from a small set of Tokens.
/// </summary>
/// <example>
/// <code language="csharp">
/// var resolver = new StyleResolver([DefaultTheme.Load(), myOverrides]);   // later Stylesheets win ties
/// </code>
/// </example>
public static class DefaultTheme
{
    /// <summary>The name that the theme's source positions report.</summary>
    public const string FileName = "default.jsonc";

    /// <summary>The name of the embedded resource that holds the theme text.</summary>
    const string ResourceName = "Silo.Themes.Default.default.jsonc";

    /// <summary>Reads the theme's JSONC text.</summary>
    /// <returns>The Stylesheet source, which can be saved as a starting point for a custom theme.</returns>
    public static string Source()
    {
        using var stream = typeof(DefaultTheme).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Parses the default theme.</summary>
    /// <returns>The theme's Stylesheet.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Source(), FileName);
}
