namespace Silo.Studio;

/// <summary>
/// The list of Previews in an assembly. Picking one shows it in its own window, run by a child process, and the buttons
/// start it again, stop it, or read the assembly again.
/// </summary>
/// <remarks>
/// <para>
/// Only one Preview runs at a time: picking another ends the one that was showing. When a Preview's process ends on its own,
/// such as when its window is closed or its method threw, the status line says so and, for a failure, why.
/// </para>
/// <para>
/// The process ends on any thread, so the panel is told how to get back to the UI thread by the <c>post</c> function.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var panel = new PreviewsPanel(new ProcessPreviewLauncher(), "MyApp.dll", () => view.Variant, "my-theme.jsonc", window.Post);
///
/// studioView.Children.Add(panel);
/// </code>
/// </example>
public sealed class PreviewsPanel : Box, IDisposable
{
    /// <summary>Starts and lists the processes.</summary>
    readonly IPreviewLauncher launcher;

    /// <summary>The assembly with the Previews.</summary>
    readonly string assemblyPath;

    /// <summary>Gets the variant that a Preview is shown in.</summary>
    readonly Func<ThemeVariant> variant;

    /// <summary>The theme file that is applied on top of the Default theme, or <see langword="null"/>.</summary>
    readonly string? theme;

    /// <summary>Runs an action on the UI thread.</summary>
    readonly Action<Action> post;

    /// <summary>The names of the Previews.</summary>
    readonly ListView previews = new() { Name = "previews" };

    /// <summary>The line that tells what happened to the shown Preview.</summary>
    readonly TextBlock status = new() { Name = "preview-status" };

    /// <summary>The names that the list shows.</summary>
    IReadOnlyList<string> names = [];

    /// <summary>The running Preview, or <see langword="null"/>.</summary>
    IPreviewProcess? running;

    /// <summary>Creates the panel and reads the assembly.</summary>
    /// <param name="launcher">What starts the child processes.</param>
    /// <param name="assemblyPath">The assembly with the Previews.</param>
    /// <param name="variant">Gets the Default theme variant that a Preview is shown in.</param>
    /// <param name="theme">The theme file to apply on top, or <see langword="null"/>.</param>
    /// <param name="post">Runs an action on the UI thread, such as <c>SdlWindow.Post</c>. Without it the action runs at once.</param>
    public PreviewsPanel(
        IPreviewLauncher launcher,
        string assemblyPath,
        Func<ThemeVariant> variant,
        string? theme = null,
        Action<Action>? post = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(variant);

        this.launcher = launcher;
        this.assemblyPath = assemblyPath;
        this.variant = variant;
        this.theme = theme;
        this.post = post ?? (action => action());

        Name = "previews-panel";

        var title = new TextBlock { Text = $"Previews in {Path.GetFileName(assemblyPath)}" };

        title.AddClass("title");
        status.AddClass("muted");

        Children.Add(title);
        Children.Add(BuildToolbar());
        Children.Add(previews);
        Children.Add(status);

        previews.SelectionChanged += (_, _) => Show();

        Reload();
    }

    /// <summary>Gets the names of the Previews that were found.</summary>
    public IReadOnlyList<string> Names => names;

    /// <summary>Gets the name of the picked Preview, or <see langword="null"/>.</summary>
    public string? SelectedName => previews.SelectedIndex >= 0 && previews.SelectedIndex < names.Count
        ? names[previews.SelectedIndex]
        : null;

    /// <summary>Gets a value indicating whether a Preview is running.</summary>
    public bool IsRunning => running is { HasExited: false };

    /// <summary>Reads the assembly again, which ends the running Preview.</summary>
    public void Reload()
    {
        Stop();

        try
        {
            names = launcher.List(assemblyPath);
        }
        catch (InvalidOperationException e)
        {
            names = [];

            status.Text = e.Message;
        }

        previews.Items = [.. names];
        previews.SelectedIndex = -1;

        if (names.Count > 0)
            status.Text = $"{names.Count} Preview(s)";
        else if (status.Text.Length == 0)
            status.Text = "No methods are marked [Preview]";
    }

    /// <summary>Shows the picked Preview in a new process, ending the one that was running.</summary>
    public void Show()
    {
        var name = SelectedName;

        Stop();

        if (name is null)
            return;

        var process = launcher.Start(assemblyPath, name, variant(), theme);

        running = process;

        status.Text = $"Showing {name}";

        process.Exited += (_, _) => post(() => OnExited(process, name));
    }

    /// <summary>Ends the running Preview.</summary>
    public void Stop()
    {
        var process = running;

        running = null;

        process?.Dispose();
    }

    /// <summary>Ends the running Preview.</summary>
    public void Dispose() => Stop();

    /// <summary>Builds the row of buttons.</summary>
    /// <returns>The row.</returns>
    Box BuildToolbar()
    {
        var bar = new Box { Name = "previews-toolbar" };
        var restart = new Button { Text = "Restart", Name = "restart-preview" };
        var stop = new Button { Text = "Stop", Name = "stop-preview" };
        var reload = new Button { Text = "Reload", Name = "reload-previews" };

        restart.Click += (_, _) => Show();
        stop.Click += (_, _) => StopAndSay();
        reload.Click += (_, _) => Reload();

        bar.Children.Add(restart);
        bar.Children.Add(stop);
        bar.Children.Add(reload);

        return bar;
    }

    /// <summary>Ends the running Preview and says so.</summary>
    void StopAndSay()
    {
        Stop();

        status.Text = "Stopped";
    }

    /// <summary>Tells how a Preview ended, unless it was replaced or stopped from here.</summary>
    /// <param name="process">The process that ended.</param>
    /// <param name="name">The name of its Preview.</param>
    void OnExited(IPreviewProcess process, string name)
    {
        if (!ReferenceEquals(process, running))
            return;

        status.Text = process.ExitCode is 0 or null
            ? $"{name} was closed"
            : $"{name} failed: {process.Error}";
    }
}
