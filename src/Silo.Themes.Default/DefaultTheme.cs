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

    /// <summary>The name that the dark variant's source positions report.</summary>
    public const string DarkFileName = "dark.jsonc";

    /// <summary>The name of the embedded resource that holds the theme text.</summary>
    const string ResourceName = "Silo.Themes.Default.default.jsonc";

    /// <summary>The name of the embedded resource that holds the dark variant's Tokens.</summary>
    const string DarkResourceName = "Silo.Themes.Default.dark.jsonc";

    /// <summary>Reads the theme's JSONC text.</summary>
    /// <returns>The Stylesheet source, which can be saved as a starting point for a custom theme.</returns>
    public static string Source() => ReadResource(ResourceName);

    /// <summary>Reads the JSONC text of the dark variant, which holds only the Tokens that differ from the light theme.</summary>
    /// <returns>The Stylesheet source, which can be saved as a starting point for a custom variant.</returns>
    public static string DarkSource() => ReadResource(DarkResourceName);

    /// <summary>Reads an embedded resource as text.</summary>
    /// <param name="name">The name of the resource.</param>
    /// <returns>The text of the resource.</returns>
    static string ReadResource(string name)
    {
        using var stream = typeof(DefaultTheme).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded resource '{name}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Parses the default theme.</summary>
    /// <returns>The theme's Stylesheet.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Source(), FileName);

    /// <summary>Parses the Stylesheets of a variant of the default theme, in the order they must be given to a resolver.</summary>
    /// <param name="variant">The color variant.</param>
    /// <returns>
    /// The theme's Stylesheet, followed for <see cref="ThemeVariant.Dark"/> by a Stylesheet that overrides its Tokens.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="variant"/> is not a defined value.</exception>
    /// <example>
    /// <code language="csharp">
    /// var resolver = new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), myOverrides]);
    /// </code>
    /// </example>
    public static Stylesheet[] Load(ThemeVariant variant) => variant switch
    {
        ThemeVariant.Light => [Load()],

        ThemeVariant.Dark => [Load(), JsoncStylesheetReader.Read(DarkSource(), DarkFileName)],

        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown theme variant."),
    };
}
