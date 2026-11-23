using Silo.Styling;

namespace Silo;

/// <summary>
/// The look of <see cref="QuantityBox"/>: an embedded Stylesheet that is loaded after the default theme.
/// </summary>
/// <example>
/// <code language="csharp">
/// var resolver = new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), QuantityTheme.Load()]);
/// </code>
/// </example>
public static class QuantityTheme
{
    /// <summary>The name that the Stylesheet's source positions report.</summary>
    public const string FileName = "quantity.jsonc";

    /// <summary>The name of the embedded resource that holds the Stylesheet text.</summary>
    const string ResourceName = "Silo.Controls.UnitsNet.quantity.jsonc";

    /// <summary>Reads the Stylesheet's JSONC text.</summary>
    /// <returns>The source, which can be saved as a starting point for a custom look.</returns>
    public static string Source()
    {
        using var stream = typeof(QuantityTheme).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Parses the Stylesheet. It uses the Tokens of the default theme, so load it after that.</summary>
    /// <returns>The Stylesheet.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Source(), FileName);
}
