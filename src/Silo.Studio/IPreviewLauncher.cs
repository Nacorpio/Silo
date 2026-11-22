namespace Silo.Studio;

/// <summary>A Preview that is running in a child process.</summary>
public interface IPreviewProcess : IDisposable
{
    /// <summary>Raised on any thread after the process ended, whether the user closed its window, it crashed, or it was stopped.</summary>
    event EventHandler? Exited;

    /// <summary>Gets a value indicating whether the process has ended.</summary>
    bool HasExited { get; }

    /// <summary>Gets what the process wrote to its error output, such as why a Preview could not be shown.</summary>
    string Error { get; }

    /// <summary>Gets the exit code, or <see langword="null"/> while the process is running.</summary>
    int? ExitCode { get; }

    /// <summary>Ends the process when it is running.</summary>
    void Stop();
}

/// <summary>Starts the child processes that list and show the Previews of an assembly.</summary>
/// <remarks>
/// The interface is the seam between Studio and the operating system, so that the panel that uses it can be tested without
/// starting processes.
/// </remarks>
/// <example>
/// <code language="csharp">
/// IPreviewLauncher launcher = new ProcessPreviewLauncher();
///
/// var names = launcher.List("MyApp.dll");
///
/// using var process = launcher.Start("MyApp.dll", names[0], ThemeVariant.Dark, "my-theme.jsonc");
/// </code>
/// </example>
public interface IPreviewLauncher
{
    /// <summary>Lists the names of the Previews in an assembly.</summary>
    /// <param name="assemblyPath">The assembly.</param>
    /// <returns>The names.</returns>
    /// <exception cref="InvalidOperationException">The assembly could not be read, with the reason as the message.</exception>
    IReadOnlyList<string> List(string assemblyPath);

    /// <summary>Shows a Preview in a window of a child process.</summary>
    /// <param name="assemblyPath">The assembly.</param>
    /// <param name="name">The name of the Preview.</param>
    /// <param name="variant">The Default theme variant.</param>
    /// <param name="theme">A theme file to apply on top, or <see langword="null"/>.</param>
    /// <returns>The running process.</returns>
    IPreviewProcess Start(string assemblyPath, string name, ThemeVariant variant, string? theme);
}
