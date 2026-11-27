using Silo.Inspector;
using Silo.Layout;
using Silo.Platform.Sdl;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo.Studio;

/// <summary>The command-line mode of Studio that opens the review window for a new Control.</summary>
/// <remarks>
/// <para>
/// <c>silo-studio --review ToolCallCard</c> shows the Control beside its questions. The questions come from
/// <c>--questions &lt;file&gt;</c>, else from the <c>review.jsonc</c> that ships with Studio for that Control, else a short
/// general set. Submit writes the answers to the <c>--out</c> folder, <c>reviews</c> by default. A review window for the same
/// Control that is already open is closed first.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var exitCode = ReviewMode.Run("ToolCallCard", questions: null, output: "reviews", screenshot: null, ThemeVariant.Dark);
/// </code>
/// </example>
public static class ReviewMode
{
    /// <summary>The exit code for a Control or a questions file that could not be used.</summary>
    public const int Failed = 2;

    /// <summary>Runs the mode.</summary>
    /// <param name="control">The name of the Control, as in the <see cref="ControlCatalog"/>.</param>
    /// <param name="questions">A questions file, or <see langword="null"/> for the one that ships with Studio.</param>
    /// <param name="output">The folder that Submit writes to.</param>
    /// <param name="screenshot">A PNG to draw the window to instead of opening it, or <see langword="null"/>.</param>
    /// <param name="variant">The theme variant.</param>
    /// <returns>The exit code: 0 for success, <see cref="Failed"/> when the Control or the file could not be used.</returns>
    public static int Run(string control, string? questions, string output, string? screenshot, ThemeVariant variant)
    {
        ReviewView view;

        try
        {
            view = new ReviewView(Load(control, questions)) { Variant = variant };
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException)
        {
            Console.Error.WriteLine(e.Message);

            return Failed;
        }

        if (screenshot is not null)
        {
            SaveScreenshot(view, screenshot);

            return 0;
        }

        using var held = ReviewLock.Acquire(control);

        Show(view, output);

        return 0;
    }

    /// <summary>Finds the questions of a Control.</summary>
    /// <param name="control">The name of the Control.</param>
    /// <param name="file">A questions file, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="FormatException">The file does not read.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static ReviewDocument Load(string control, string? file)
    {
        if (file is not null)
            return ReviewDocument.Parse(File.ReadAllText(file));

        using var stream = typeof(ReviewMode).Assembly.GetManifestResourceStream($"Silo.Studio.Reviews.{control}.review.jsonc");

        if (stream is null)
            return General(control);

        using var reader = new StreamReader(stream);

        return ReviewDocument.Parse(reader.ReadToEnd());
    }

    /// <summary>Gets the short set of questions that any Control gets when it has none of its own.</summary>
    /// <param name="control">The name of the Control.</param>
    /// <returns>The document.</returns>
    static ReviewDocument General(string control) => new(
        control,
        $"{control} review",
        string.Empty,
        [
            new("look", ReviewQuestionKind.Rating, "How does it look?", string.Empty, [], null, null),
            new("feel", ReviewQuestionKind.Rating, "How does it feel to use?", "Try the mouse and the keyboard.", [], null, null),
            new("states", ReviewQuestionKind.YesNo, "Do all of its states look right?", "Use the state picker above the Control.", [], null, null),
            new("missing", ReviewQuestionKind.Text, "What is missing or feels wrong?", string.Empty, [], null, null),
        ]);

    /// <summary>Opens the window and keeps it styled until it is closed.</summary>
    /// <param name="view">The review view.</param>
    /// <param name="output">The folder for the answers.</param>
    static void Show(ReviewView view, string output)
    {
        using var window = new SdlWindow(view) { Styles = Styles(view.Variant) };
        using var attachment = window.AttachDiagnostics();

        view.VariantChanged += (_, _) => window.Styles = Styles(view.Variant);
        view.Panel.UseClipboard(new SdlClipboard());
        view.Submitted += (_, _) =>
        {
            var path = view.Answers.Save(output, DateTimeOffset.Now);

            view.Panel.ShowStatus($"Saved to {path}");
        };

        window.Run();
    }

    /// <summary>Gets the Stylesheets of the review window.</summary>
    /// <param name="variant">The theme variant.</param>
    /// <returns>The resolver.</returns>
    public static StyleResolver Styles(ThemeVariant variant)
        => new([.. DefaultTheme.Load(variant), QuantityTheme.Load(), AgentsTheme.Load(), ChatTheme.Load(), StudioStyles.Load()]);

    /// <summary>Draws the window to a PNG file.</summary>
    /// <param name="view">The review view.</param>
    /// <param name="path">The file to write.</param>
    static void SaveScreenshot(ReviewView view, string path)
    {
        view.TextMeasurer = new SkiaTextMeasurer();

        view.ApplyStyles(Styles(view.Variant));
        view.Arrange(new Size(view.InitialWidth, view.InitialHeight));

        var builder = new DisplayListBuilder();

        view.Render(builder);

        using var bitmap = SkiaRenderer.Render(builder.Build(), view.InitialWidth, view.InitialHeight, new Color(255, 255, 255));

        File.WriteAllBytes(path, SkiaRenderer.Encode(bitmap));
    }
}
