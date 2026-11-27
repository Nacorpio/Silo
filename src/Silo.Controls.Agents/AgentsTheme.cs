using Silo.Styling;

namespace Silo;

/// <summary>
/// The Stylesheet that styles the Controls of Silo.Controls.Agents.
/// </summary>
/// <remarks>Load it after the default theme, as in <c>[.. DefaultTheme.Load(variant), AgentsTheme.Load()]</c>.</remarks>
public static class AgentsTheme
{
    /// <summary>The file name that errors report.</summary>
    public const string FileName = "agents.jsonc";

    /// <summary>The name of the embedded resource that holds the Stylesheet.</summary>
    const string ResourceName = "Silo.Controls.Agents.agents.jsonc";

    /// <summary>Reads the Stylesheet text.</summary>
    /// <returns>The JSONC source.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing from the assembly.</exception>
    public static string Source()
    {
        using var stream = typeof(AgentsTheme).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Reads the Stylesheet.</summary>
    /// <returns>The parsed Stylesheet.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Source(), FileName);
}
