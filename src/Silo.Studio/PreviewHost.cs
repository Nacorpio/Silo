using Silo.Styling;

namespace Silo.Studio;

/// <summary>Puts a Preview in a Window and works out how it is styled.</summary>
/// <remarks>
/// <para>
/// A Preview is shown with the Default theme, the Studio-independent Stylesheets only, and optionally the theme file that is
/// being edited on top, so what a user changes in a theme shows up in the Preview.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var window = PreviewHost.Wrap(catalog.Create("Primary button"), "Primary button");
///
/// var styles = PreviewHost.Styles(ThemeVariant.Dark, "my-theme.jsonc");
/// </code>
/// </example>
public static class PreviewHost
{
    /// <summary>Gets the Window that shows an Element.</summary>
    /// <param name="element">The Element that a Preview returned.</param>
    /// <param name="title">The title for the Window, when the Element is not a Window itself.</param>
    /// <returns><paramref name="element"/> when it is a Window, otherwise a new Window that holds it.</returns>
    public static Window Wrap(Element element, string title)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element is Window window)
            return window;

        var wrapper = new Window { Title = title };

        wrapper.Children.Add(element);

        return wrapper;
    }

    /// <summary>Gets the Stylesheets for a Preview.</summary>
    /// <param name="variant">The Default theme variant.</param>
    /// <param name="themePath">A theme file to apply on top, or <see langword="null"/>.</param>
    /// <returns>The resolver.</returns>
    /// <remarks>A theme file that does not read is left out, so a half-typed edit does not close the Preview.</remarks>
    public static StyleResolver Styles(ThemeVariant variant, string? themePath)
    {
        var sheets = new List<Stylesheet>(DefaultTheme.Load(variant));

        if (themePath is not null && File.Exists(themePath))
        {
            try
            {
                sheets.Add(JsoncStylesheetReader.Read(File.ReadAllText(themePath), themePath));
            }
            catch (StylesheetException)
            {
                // The file is left out until it reads again.
            }
        }

        return new(sheets);
    }
}
