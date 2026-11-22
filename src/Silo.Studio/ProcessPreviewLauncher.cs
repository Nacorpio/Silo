using System.Diagnostics;
using System.Reflection;

namespace Silo.Studio;

/// <summary>Runs Studio itself in <see cref="PreviewMode"/> as a child process.</summary>
/// <remarks>
/// A child process keeps the user's code away from Studio: a Preview that throws, hangs or crashes ends its own window and
/// nothing else.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var launcher = new ProcessPreviewLauncher();
///
/// var names = launcher.List("MyApp.dll");
/// </code>
/// </example>
public sealed class ProcessPreviewLauncher : IPreviewLauncher
{
    /// <inheritdoc/>
    public IReadOnlyList<string> List(string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);

        using var process = Process.Start(Create(["--assembly", assemblyPath, "--list-previews"]))
            ?? throw new InvalidOperationException("Studio could not start a child process.");

        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(error.Result.Trim());

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <inheritdoc/>
    public IPreviewProcess Start(string assemblyPath, string name, ThemeVariant variant, string? theme)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(name);

        List<string> arguments = ["--assembly", assemblyPath, "--preview", name];

        if (variant == ThemeVariant.Dark)
            arguments.Add("--dark");

        if (theme is not null)
            arguments.AddRange(["--theme", theme]);

        return new ChildProcess(Process.Start(Create(arguments))
            ?? throw new InvalidOperationException("Studio could not start a child process."));
    }

    /// <summary>Builds the start info that runs this program again with arguments.</summary>
    /// <param name="arguments">The arguments for Studio.</param>
    /// <returns>The start info, with the output redirected.</returns>
    static ProcessStartInfo Create(IReadOnlyList<string> arguments)
    {
        var host = Environment.ProcessPath ?? throw new InvalidOperationException("The path of Studio is not known.");

        var info = new ProcessStartInfo(host)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (Path.GetFileNameWithoutExtension(host) == "dotnet")
            info.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);

        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        return info;
    }

    /// <summary>A running child process.</summary>
    sealed class ChildProcess : IPreviewProcess
    {
        /// <summary>The process.</summary>
        readonly Process process;

        /// <summary>What was written to the error output so far.</summary>
        readonly System.Text.StringBuilder error = new();

        /// <summary>Starts watching a process.</summary>
        /// <param name="process">The process that was started.</param>
        public ChildProcess(Process process)
        {
            this.process = process;

            process.ErrorDataReceived += (_, e) =>
            {
                lock (error)
                    error.AppendLine(e.Data);
            };

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        /// <inheritdoc/>
        public event EventHandler? Exited;

        /// <inheritdoc/>
        public bool HasExited => process.HasExited;

        /// <inheritdoc/>
        public int? ExitCode => process.HasExited ? process.ExitCode : null;

        /// <inheritdoc/>
        public string Error
        {
            get
            {
                lock (error)
                    return error.ToString().Trim();
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process ended in the meantime.
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Stop();

            process.Dispose();
        }
    }
}
