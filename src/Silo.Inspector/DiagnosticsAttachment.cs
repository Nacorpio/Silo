using Silo.Diagnostics;
using Silo.Platform.Sdl;

namespace Silo.Inspector;

/// <summary>
/// Makes an <see cref="SdlWindow"/> inspectable: it serves the window's Element tree over a named pipe and opens the
/// Inspector in a second window when the user presses F12.
/// </summary>
/// <remarks>
/// The Inspector window runs on the same thread and event loop as the app window, and talks to the app over the pipe,
/// exactly as an Inspector in another process would. Pressing F5 in the Inspector window fetches the tree again.
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var window = new SdlWindow("My app", 640, 360, root);
/// using var attachment = window.AttachDiagnostics();
///
/// window.Run();
/// </code>
/// </example>
public sealed class DiagnosticsAttachment : IDisposable
{
    /// <summary>The window that is inspected.</summary>
    readonly SdlWindow app;

    /// <summary>Serves the app's tree.</summary>
    readonly DiagnosticsServer server;

    /// <summary>The Inspector window, or <see langword="null"/> while none is open.</summary>
    SdlWindow? inspector;

    /// <summary>The connection of the open Inspector window.</summary>
    DiagnosticsClient? client;

    /// <summary>Starts serving a window.</summary>
    /// <param name="app">The window to inspect.</param>
    /// <param name="pipeName">The pipe name, or <see langword="null"/> for the default.</param>
    internal DiagnosticsAttachment(SdlWindow app, string? pipeName)
    {
        this.app = app;

        var agent = new DiagnosticsAgent(app.Root, () => app.Styles);

        server = new DiagnosticsServer(agent, RunOnUiThread, pipeName);

        server.Start();

        app.KeyPressed += OnAppKey;
    }

    /// <summary>Gets the name of the pipe that the app is served on.</summary>
    public string PipeName => server.PipeName;

    /// <summary>Gets the Inspector window, or <see langword="null"/> while none is open.</summary>
    public SdlWindow? InspectorWindow => inspector;

    /// <summary>Opens the Inspector in a second window, or returns the one that is already open. Call it on the window's thread.</summary>
    /// <returns>The Inspector window.</returns>
    public SdlWindow OpenInspector()
    {
        if (inspector is not null)
            return inspector;

        var view = new InspectorView();
        var window = new SdlWindow("Silo Inspector", 900, 500, view);

        inspector = window;

        window.Closed += (_, _) => app.Post(() => CloseInspector(window));

        _ = ConnectAsync(window, view);

        return window;
    }

    /// <summary>Stops serving and closes the Inspector window.</summary>
    public void Dispose()
    {
        app.KeyPressed -= OnAppKey;

        server.Dispose();

        if (inspector is { } window)
            CloseInspector(window);
    }

    /// <summary>Runs a request on the app window's thread.</summary>
    /// <param name="work">The work to run.</param>
    /// <returns>A task with the result of the work.</returns>
    Task<string?> RunOnUiThread(Func<string?> work)
    {
        var source = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        app.Post(() =>
        {
            try
            {
                source.SetResult(work());
            }
            catch (Exception error)
            {
                source.SetException(error);
            }
        });

        return source.Task;
    }

    /// <summary>Opens the Inspector when F12 is pressed in the app window.</summary>
    /// <param name="key">The key.</param>
    void OnAppKey(Key key)
    {
        if (key == Key.F12)
            OpenInspector();
    }

    /// <summary>Connects the Inspector window to the app and shows the tree.</summary>
    /// <param name="window">The Inspector window.</param>
    /// <param name="view">Its view.</param>
    async Task ConnectAsync(SdlWindow window, InspectorView view)
    {
        var connection = await DiagnosticsClient.ConnectAsync(PipeName);
        var controller = new InspectorController(connection, view, window.Post);

        if (inspector != window)
        {
            await connection.DisposeAsync();

            return;
        }

        client = connection;

        window.KeyPressed += key =>
        {
            if (key == Key.F5)
                _ = controller.RefreshAsync();
        };

        await controller.RefreshAsync();
    }

    /// <summary>Disposes the Inspector window and its connection.</summary>
    /// <param name="window">The Inspector window.</param>
    void CloseInspector(SdlWindow window)
    {
        if (inspector != window)
            return;

        inspector = null;

        _ = client?.DisposeAsync();

        client = null;

        window.Dispose();
    }
}
