using Silo;
using Silo.Inspector;
using Silo.Layout;
using Silo.Platform.Sdl;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Studio;
using Silo.Styling;
using Silo.Styling.Values;

// Usage: silo-studio [--dark] [--theme <file.jsonc>] [--screenshot <out.png>]
// Shows every Control in every Pseudo-state with the default theme. With --theme the file is applied on top of the default
// theme and watched, so the window restyles whenever it is saved. With --screenshot the gallery is drawn to a PNG without
// opening a window. F12 opens the Inspector in a second window.
var theme = ArgumentAfter("--theme");
var screenshot = ArgumentAfter("--screenshot");

var view = new StudioView { Variant = args.Contains("--dark") ? ThemeVariant.Dark : ThemeVariant.Light };
var overrides = theme is null ? null : JsoncStylesheetReader.Read(File.ReadAllText(theme), Path.GetFileName(theme));

StyleResolver Resolve(Stylesheet? custom)
{
    var sheets = new List<Stylesheet>([.. DefaultTheme.Load(view.Variant), StudioStyles.Load()]);

    if (custom is not null)
        sheets.Add(custom);

    return new(sheets);
}

if (screenshot is not null)
{
    SaveScreenshot(screenshot);

    return;
}

using var window = new SdlWindow(view) { Styles = Resolve(overrides) };
using var attachment = window.AttachDiagnostics();
using var watcher = theme is null ? null : new StylesheetWatcher(theme);

view.VariantChanged += (_, _) => window.Styles = Resolve(overrides);

if (watcher is not null)
{
    watcher.Reloaded += sheet =>
    {
        overrides = sheet;
        window.Post(() => window.Styles = Resolve(sheet));
    };

    watcher.Failed += error => Console.Error.WriteLine(error.Message);
}

window.Run();

string? ArgumentAfter(string name)
{
    var index = Array.IndexOf(args, name);

    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

void SaveScreenshot(string path)
{
    view.TextMeasurer = new SkiaTextMeasurer();

    view.ApplyStyles(Resolve(overrides));
    view.Arrange(new Size(view.InitialWidth, view.InitialHeight));

    var builder = new DisplayListBuilder();

    view.Render(builder);

    using var bitmap = SkiaRenderer.Render(builder.Build(), view.InitialWidth, view.InitialHeight, new Color(255, 255, 255));

    File.WriteAllBytes(path, SkiaRenderer.Encode(bitmap));
}
