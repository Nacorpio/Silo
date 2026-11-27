using System.Diagnostics;

namespace Silo.Studio;

/// <summary>Makes sure that only one review window shows a Control: opening a second one closes the first.</summary>
/// <remarks>
/// <para>
/// The window of a review writes its process id to <c>&lt;folder&gt;/&lt;control&gt;.pid</c>. A new window for the same Control
/// reads the file first and closes the process that it names, and only when that process is still alive and is another copy
/// of this program, so a stale file never closes an unrelated program. Disposing the lock removes the file when it is still
/// the one that this window wrote.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var review = ReviewLock.Acquire("ToolCallCard");
///
/// window.Run();
/// </code>
/// </example>
public sealed class ReviewLock : IDisposable
{
    /// <summary>The file that holds the process id.</summary>
    readonly string path;

    /// <summary>The process id that this lock wrote.</summary>
    readonly int id;

    /// <summary>Creates the lock.</summary>
    /// <param name="path">The file.</param>
    /// <param name="id">The process id that it holds.</param>
    /// <param name="closed">The process id of the window that was closed to take the lock, or <see langword="null"/>.</param>
    ReviewLock(string path, int id, int? closed)
    {
        this.path = path;
        this.id = id;

        Closed = closed;
    }

    /// <summary>Gets the process id of the window that was closed to take the lock, or <see langword="null"/> when there was none.</summary>
    public int? Closed { get; }

    /// <summary>Takes the lock for a Control and closes the window that held it.</summary>
    /// <param name="control">The name of the Control.</param>
    /// <param name="folder">The folder for the files, or <see langword="null"/> for the temporary folder of the user.</param>
    /// <param name="id">The process id to record, or <see langword="null"/> for this process.</param>
    /// <param name="close">Closes a process, or <see langword="null"/> to close a copy of this program.</param>
    /// <returns>The lock, which is released when it is disposed.</returns>
    public static ReviewLock Acquire(string control, string? folder = null, int? id = null, Action<int>? close = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(control);

        folder ??= Path.Combine(Path.GetTempPath(), "silo-review");

        Directory.CreateDirectory(folder);

        var own = id ?? Environment.ProcessId;
        var path = Path.Combine(folder, $"{control}.pid");

        int? closed = null;

        if (File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out var previous) && previous != own)
        {
            (close ?? CloseCopy)(previous);

            closed = previous;
        }

        File.WriteAllText(path, own.ToString());

        return new(path, own, closed);
    }

    /// <summary>Releases the lock.</summary>
    public void Dispose()
    {
        if (File.Exists(path) && File.ReadAllText(path).Trim() == id.ToString())
            File.Delete(path);
    }

    /// <summary>Closes a process when it is another copy of this program.</summary>
    /// <param name="processId">The process id.</param>
    static void CloseCopy(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var self = Process.GetCurrentProcess();

            if (process.ProcessName == self.ProcessName)
                process.Kill();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process has already ended.
        }
    }
}
