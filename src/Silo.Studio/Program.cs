using Silo;
using Silo.Inspector;
using Silo.Layout;
using Silo.Platform.Sdl;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Studio;
using Silo.Styling;
using Silo.Styling.Values;

// Usage: silo-studio [--dark] [--text] [--custom-titlebar] [--dividers] [--page <category>] [--theme <file.jsonc>] [--screenshot <out.png>]
//        silo-studio --terminal [<program>] [--dark]
//        silo-studio --assembly <app.dll> --list-previews
//        silo-studio --assembly <app.dll> --preview <name> [--dark] [--theme <file.jsonc>] [--screenshot <out.png>]
// Shows every Control in every Pseudo-state with the default theme. With --theme the file is applied on top of the default
// theme and opened in the editor beside the gallery: edit a Rule's properties and the gallery restyles as you type, then
// press Save. The file is watched, so a change made in another editor is loaded unless Studio has unsaved edits. With
// --text the editor shows the file as text instead of as Rules; the Visual and Text buttons switch while it runs. With
// --screenshot the gallery is drawn to a PNG without opening a window. The gallery has a tab page for each category of
// Controls, and --page picks the one that is shown, such as "Text input" (the first page by default). F12 opens the Inspector in a second window.
// With --assembly alone, Studio lists the methods marked [Preview] in the assembly beside the gallery; picking one shows it
// in its own window with the Default theme and the --theme file on top. Studio runs --list-previews and --preview in a
// child process, so the user's code stays out of Studio, and those two can be used from a terminal as well.
var theme = ArgumentAfter("--theme");
var screenshot = ArgumentAfter("--screenshot");
var assembly = ArgumentAfter("--assembly");

var previewMode = args.Contains("--list-previews") || ArgumentAfter("--preview") is not null;

if (assembly is not null && previewMode)
    return PreviewMode.Run(assembly, args.Contains("--list-previews"), ArgumentAfter("--preview"), theme, screenshot,
        args.Contains("--dark") ? ThemeVariant.Dark : ThemeVariant.Light);

// With --terminal Studio opens a terminal window instead of the gallery: a console with a few demo commands, or the program
// that follows the flag, such as "bash", run in it. It is a quick way to try the Terminal and the sessions in a real window.
if (args.Contains("--terminal"))
{
    var program = ArgumentAfter("--terminal");
    var variant = args.Contains("--dark") ? ThemeVariant.Dark : ThemeVariant.Light;

    ITerminalSession session;

    if (program is not null && !program.StartsWith("--", StringComparison.Ordinal))
        session = new ProcessSession(program);
    else
    {
        var console = new CommandSession { Banner = "Silo console. Type help for the commands.\n", Prompt = "\u001b[32msilo\u001b[0m$ " };

        console.Add("hello", "Greets you", context => context.WriteLine($"Hello {string.Join(' ', context.Arguments)}!"));
        console.Add("colors", "Shows the 16 colors", context =>
        {
            for (var i = 0; i < 16; i++)
                context.Write($"\u001b[{(i < 8 ? 40 + i : 92 + i)}m  \u001b[0m");

            context.WriteLine();
        });
        console.Add("count", "Counts up, press Ctrl+C to stop", async context =>
        {
            for (var i = 1; i <= 20; i++)
            {
                context.WriteLine(i.ToString());

                await Task.Delay(200, context.CancellationToken);
            }
        });

        session = console;
    }

    using var terminalWindow = new TerminalWindow(session, "Silo terminal", styles: new StyleResolver(DefaultTheme.Load(variant)));

    terminalWindow.Run();

    return 0;
}

var model = theme is null ? null : OpenTheme(theme);

var view = new StudioView(model) { Variant = args.Contains("--dark") ? ThemeVariant.Dark : ThemeVariant.Light };

if (ArgumentAfter("--page") is { } page)
    view.Matrix.ShowCategory(page);

if (view.Editor is not null)
    view.Editor.TextMode = args.Contains("--text");

view.Categories.ShowDividers = args.Contains("--dividers");

if (int.TryParse(ArgumentAfter("--height"), out var height))
    view.InitialHeight = height;

var customTitleBar = args.Contains("--custom-titlebar");

if (customTitleBar)
    view.ShowTitleBar();

StyleResolver Resolve()
{
    var sheets = new List<Stylesheet>([.. DefaultTheme.Load(view.Variant), QuantityTheme.Load(), AgentsTheme.Load(), StudioStyles.Load()]);

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

    return 0;
}

SdlWindow? shown = null;

using var previews = assembly is null
    ? null
    : new PreviewsPanel(new ProcessPreviewLauncher(), assembly, () => view.Variant, theme, work => shown!.Post(work));

if (previews is not null)
    view.AddPanel(previews);

using var window = new SdlWindow(view) { Styles = Resolve(), CustomTitleBar = customTitleBar };
using var attachment = window.AttachDiagnostics();
using var watcher = theme is null ? null : new StylesheetWatcher(theme);

shown = window;

view.Matrix.Playground.Clipboard = new SdlClipboard();

if (view.Editor is not null)
    view.Editor.Clipboard = new SdlClipboard();

view.VariantChanged += (_, _) => window.Styles = Resolve();

if (previews is not null)
    view.VariantChanged += (_, _) => RestartPreview();

if (model is not null)
    model.Changed += (_, _) => window.Styles = Resolve();

if (watcher is not null)
{
    watcher.Reloaded += sheet => window.Post(() => Reload(sheet.Source));
    watcher.Failed += error => Console.Error.WriteLine(error.Message);
}

window.Run();

return 0;

void RestartPreview()
{
    if (previews is { IsRunning: true })
        previews.Show();
}

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
