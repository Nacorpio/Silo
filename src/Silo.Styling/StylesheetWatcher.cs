namespace Silo.Styling;

/// <summary>
/// Watches a Stylesheet file and reads it again whenever it is saved, which is what makes hot reload work.
/// </summary>
/// <remarks>
/// <para>
/// Editors often save a file as several writes, so the watcher waits for <see cref="Delay"/> of quiet before it reads. A
/// file that does not parse raises <see cref="Failed"/> and the last good Stylesheet stays in use; fixing the file raises
/// <see cref="Reloaded"/> again.
/// </para>
/// <para>Both events are raised on a thread-pool thread. Hand the result to the UI thread before applying it.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var watcher = new StylesheetWatcher("theme.jsonc");
///
/// watcher.Reloaded += sheet => ui.Post(() => root.ApplyStyles(new StyleResolver([sheet])));
/// watcher.Failed += error => Console.Error.WriteLine(error.Message);
/// </code>
/// </example>
public sealed class StylesheetWatcher : IDisposable
{
    /// <summary>The path of the watched file, as given.</summary>
    readonly string path;

    /// <summary>The watcher for the file's directory.</summary>
    readonly FileSystemWatcher watcher;

    /// <summary>Runs <see cref="Reload"/> after the quiet period, and is pushed back by every new change.</summary>
    readonly Timer timer;

    /// <summary>Creates a watcher and starts watching.</summary>
    /// <param name="path">The path of the Stylesheet file.</param>
    public StylesheetWatcher(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        this.path = Path.GetFullPath(path);

        timer = new Timer(_ => Reload());

        watcher = new FileSystemWatcher(Path.GetDirectoryName(this.path)!, Path.GetFileName(this.path))
        {
            NotifyFilter =
                NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };

        watcher.Changed += (_, _) => Schedule();
        watcher.Created += (_, _) => Schedule();
        watcher.Renamed += (_, _) => Schedule();
        watcher.EnableRaisingEvents = true;
    }

    /// <summary>Raised with the new Stylesheet after the file was saved and parsed.</summary>
    public event Action<Stylesheet>? Reloaded;

    /// <summary>Raised when the saved file could not be read or parsed.</summary>
    public event Action<Exception>? Failed;

    /// <summary>Gets or sets how long the file has to stay unchanged before it is read. The default is 100 ms.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Reads and parses the file now, without waiting for a change.</summary>
    /// <returns>The Stylesheet.</returns>
    /// <exception cref="StylesheetException">The file does not parse.</exception>
    public Stylesheet Read() => JsoncStylesheetReader.Read(File.ReadAllText(path), Path.GetFileName(path));

    /// <summary>Stops watching.</summary>
    public void Dispose()
    {
        watcher.Dispose();
        timer.Dispose();
    }

    /// <summary>Restarts the quiet period.</summary>
    void Schedule() => timer.Change(Delay, Timeout.InfiniteTimeSpan);

    /// <summary>Reads the file and raises <see cref="Reloaded"/> or <see cref="Failed"/>.</summary>
    void Reload()
    {
        try
        {
            var sheet = Read();

            Reloaded?.Invoke(sheet);
        }
        catch (Exception error) when (error is StylesheetException or IOException or UnauthorizedAccessException)
        {
            Failed?.Invoke(error);
        }
    }
}
