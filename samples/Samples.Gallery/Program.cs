using Silo;
using Silo.Layout;
using Silo.Platform.Sdl;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Styling;
using Silo.Styling.Values;

// Usage:
//   dotnet run --project samples/Samples.Gallery -- [--theme <file.jsonc>] [--screenshot <out.png>]
// With --theme the file is watched and the window restyles when it is saved. With --screenshot the gallery is drawn to a
// PNG without opening a window.
var theme = ArgumentAfter("--theme");
var screenshot = ArgumentAfter("--screenshot");

var root = Gallery.Build();
var overrides = theme is null ? null : JsoncStylesheetReader.Read(File.ReadAllText(theme), Path.GetFileName(theme));

StyleResolver Resolve(Stylesheet? custom) => new(custom is null ? [DefaultTheme.Load(), Gallery.Styles()] : [DefaultTheme.Load(), Gallery.Styles(), custom]);

if (screenshot is not null)
{
    SaveScreenshot(screenshot, Resolve(overrides));

    return;
}

using var window = new SdlWindow("Silo gallery", 640, 360, root) { Styles = Resolve(overrides) };
using var watcher = theme is null ? null : new StylesheetWatcher(theme);

if (watcher is not null)
{
    watcher.Reloaded += sheet => window.Post(() => window.Styles = Resolve(sheet));
    watcher.Failed += error => Console.Error.WriteLine(error.Message);
}

window.Run();

string? ArgumentAfter(string name)
{
    var index = Array.IndexOf(args, name);

    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

void SaveScreenshot(string path, StyleResolver styles)
{
    root.TextMeasurer = new SkiaTextMeasurer();

    root.ApplyStyles(styles);
    root.Arrange(new Size(640, 360));

    var builder = new DisplayListBuilder();

    root.Render(builder);

    using var bitmap = SkiaRenderer.Render(builder.Build(), 640, 360, new Color(255, 255, 255));

    File.WriteAllBytes(path, SkiaRenderer.Encode(bitmap));
}
