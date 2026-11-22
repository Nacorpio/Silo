using Silo.Inspector;
using Silo.Layout;
using Silo.Platform.Sdl;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo.Studio;

/// <summary>The command-line mode of Studio that lists the Previews in an assembly and shows one of them.</summary>
/// <remarks>
/// <para>
/// Studio starts this mode in a child process, so that the code of a Preview, which is the user's code, can throw, hang or
/// crash without taking Studio with it. The Preview is shown in its own window, which watches the theme file and restyles
/// when it changes, and has the Inspector on F12 like any Silo app.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var exitCode = PreviewMode.Run("MyApp.dll", list: true, preview: null, theme: null, screenshot: null, ThemeVariant.Light);
/// </code>
/// </example>
public static class PreviewMode
{
    /// <summary>The exit code for a Preview or an assembly that could not be used.</summary>
    public const int Failed = 2;

    /// <summary>Runs the mode.</summary>
    /// <param name="assemblyPath">The assembly with the Previews.</param>
    /// <param name="list">Whether to print the names of the Previews and return.</param>
    /// <param name="preview">The name of the Preview to show.</param>
    /// <param name="theme">A theme file to apply on top of the Default theme, or <see langword="null"/>.</param>
    /// <param name="screenshot">A PNG to draw the Preview to instead of opening a window, or <see langword="null"/>.</param>
    /// <param name="variant">The Default theme variant.</param>
    /// <returns>The exit code: 0 for success, <see cref="Failed"/> when the assembly or the Preview could not be used.</returns>
    public static int Run(
        string assemblyPath,
        bool list,
        string? preview,
        string? theme,
        string? screenshot,
        ThemeVariant variant)
    {
        PreviewCatalog catalog;

        try
        {
            catalog = PreviewCatalog.Load(assemblyPath);
        }
        catch (Exception e) when (e is IOException or BadImageFormatException)
        {
            Console.Error.WriteLine(e.Message);

            return Failed;
        }

        if (list)
        {
            foreach (var info in catalog.Previews)
                Console.WriteLine(info.Name);

            return 0;
        }

        if (preview is null)
        {
            Console.Error.WriteLine("Choose a Preview with --preview <name>, or list them with --list-previews.");

            return Failed;
        }

        Window window;

        try
        {
            window = PreviewHost.Wrap(catalog.Create(preview), preview);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(e.Message);

            return Failed;
        }

        if (screenshot is not null)
        {
            SaveScreenshot(window, PreviewHost.Styles(variant, theme), screenshot);

            return 0;
        }

        Show(window, variant, theme);

        return 0;
    }

    /// <summary>Opens the window and keeps it styled until it is closed.</summary>
    /// <param name="content">The Window with the Preview.</param>
    /// <param name="variant">The Default theme variant.</param>
    /// <param name="theme">The theme file, or <see langword="null"/>.</param>
    static void Show(Window content, ThemeVariant variant, string? theme)
    {
        using var window = new SdlWindow(content) { Styles = PreviewHost.Styles(variant, theme) };
        using var attachment = window.AttachDiagnostics();
        using var watcher = theme is null ? null : new StylesheetWatcher(theme);

        if (watcher is not null)
            watcher.Reloaded += _ => window.Post(() => window.Styles = PreviewHost.Styles(variant, theme));

        window.Run();
    }

    /// <summary>Draws the Window to a PNG file.</summary>
    /// <param name="content">The Window.</param>
    /// <param name="styles">The Stylesheets.</param>
    /// <param name="path">The file to write.</param>
    static void SaveScreenshot(Window content, StyleResolver styles, string path)
    {
        content.TextMeasurer = new SkiaTextMeasurer();

        content.ApplyStyles(styles);
        content.Arrange(new Size(content.InitialWidth, content.InitialHeight));

        var builder = new DisplayListBuilder();

        content.Render(builder);

        var list = builder.Build();

        var white = new Color(255, 255, 255);

        using var bitmap = SkiaRenderer.Render(list, content.InitialWidth, content.InitialHeight, white);

        File.WriteAllBytes(path, SkiaRenderer.Encode(bitmap));
    }
}
