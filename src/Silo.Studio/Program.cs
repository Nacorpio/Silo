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
// theme and opened in the editor beside the gallery: edit a Rule's properties and the gallery restyles as you type, then
// press Save. The file is watched, so a change made in another editor is loaded unless Studio has unsaved edits. With
// --screenshot the gallery is drawn to a PNG without opening a window. F12 opens the Inspector in a second window.
var theme = ArgumentAfter("--theme");
var screenshot = ArgumentAfter("--screenshot");

var model = theme is null ? null : OpenTheme(theme);

var view = new StudioView(model) { Variant = args.Contains("--dark") ? ThemeVariant.Dark : ThemeVariant.Light };

StyleResolver Resolve()
{
    var sheets = new List<Stylesheet>([.. DefaultTheme.Load(view.Variant), StudioStyles.Load()]);

    try
    {
        if (model is not null)
            sheets.Add(model.Document.ToStylesheet());
    }
    catch (StylesheetException)
    {
        // A half-typed value can leave a Stylesheet that does not read. The theme is left out until it does.
    }

    return new(sheets);
}

if (screenshot is not null)
{
    SaveScreenshot(screenshot);

    return;
}

using var window = new SdlWindow(view) { Styles = Resolve() };
using var attachment = window.AttachDiagnostics();
using var watcher = theme is null ? null : new StylesheetWatcher(theme);

view.VariantChanged += (_, _) => window.Styles = Resolve();

if (model is not null)
    model.Changed += (_, _) => window.Styles = Resolve();

if (watcher is not null)
{
    watcher.Reloaded += sheet => window.Post(() => Reload(sheet.Source));
    watcher.Failed += error => Console.Error.WriteLine(error.Message);
}

window.Run();

void Reload(string text)
{
    if (model is not null && !model.TryReload(text) && model.IsDirty)
    {
        Console.Error.WriteLine("The theme file changed on disk.");
        Console.Error.WriteLine("The unsaved edits in Studio were kept; save or undo them first.");
    }
}

ThemeEditorModel OpenTheme(string path)
{
    var document = JsoncDocument.Parse(File.ReadAllText(path), path);

    return new(document, DefaultTheme.Load(ThemeVariant.Dark), path);
}

string? ArgumentAfter(string name)
{
    var index = Array.IndexOf(args, name);

    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

void SaveScreenshot(string path)
{
    view.TextMeasurer = new SkiaTextMeasurer();

    view.ApplyStyles(Resolve());
    view.Arrange(new Size(view.InitialWidth, view.InitialHeight));

    var builder = new DisplayListBuilder();

    view.Render(builder);

    var list = builder.Build();

    using var bitmap = SkiaRenderer.Render(list, view.InitialWidth, view.InitialHeight, new Color(255, 255, 255));

    File.WriteAllBytes(path, SkiaRenderer.Encode(bitmap));
}
