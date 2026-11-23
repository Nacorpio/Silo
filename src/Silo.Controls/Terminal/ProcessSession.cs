using System.Diagnostics;

namespace Silo;

/// <summary>
/// A session that runs a program and connects its standard input, output and error to a <see cref="Terminal"/>.
/// </summary>
/// <remarks>
/// <para>
/// The program is started with redirected pipes, not with a pseudo terminal, so it does not know it talks to a terminal: many
/// programs then print without colors, shells do not show a prompt, and full-screen programs such as <c>vim</c> and <c>htop</c> do not work.
/// A real pseudo terminal comes later. Everything that runs fine in a pipe works, including builds, scripts and command line tools.
/// </para>
/// <para>
/// Because a pipe has no line discipline, the session does it: what the user types is edited with a <see cref="TerminalLineEditor"/>,
/// echoed to the terminal when <see cref="Echo"/> is on, and sent to the program a line at a time. Ctrl+C ends the program and its
/// child processes, and Ctrl+D on an empty line closes its input. The output of the program and its error output are shown together, with line feeds
/// turned into line feeds with a carriage return. The environment variables <c>COLUMNS</c> and <c>LINES</c> hold the size of the terminal when the program starts.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var session = new ProcessSession("dotnet", ["--info"]);
///
/// terminal.Attach(session, work => window.Post(work));
/// </code>
/// </example>
public sealed class ProcessSession : ITerminalSession
{
    readonly ProcessStartInfo startInfo;

    readonly TerminalLineEditor editor = new();

    readonly object gate = new();

    Process? process;

    bool disposed;

    /// <summary>Creates a session for a program.</summary>
    /// <param name="fileName">The program, found on the PATH if it has no directory.</param>
    /// <param name="arguments">The arguments.</param>
    public ProcessSession(string fileName, IEnumerable<string>? arguments = null)
        : this(CreateStartInfo(fileName, arguments))
    {
    }

    /// <summary>Creates a session for a program with all the start settings. The redirections are set by the session.</summary>
    /// <param name="startInfo">The start settings, with the working directory and environment variables if they are needed.</param>
    public ProcessSession(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        this.startInfo = startInfo;
    }

    /// <inheritdoc/>
    public event EventHandler<TerminalOutputEventArgs>? Output;

    /// <inheritdoc/>
    public event EventHandler<TerminalExitedEventArgs>? Exited;

    /// <summary>Gets or sets whether typed text is shown on the terminal, since the program does not echo it. The default is <see langword="true"/>.</summary>
    public bool Echo { get; set; } = true;

    /// <summary>Gets the exit code of the program, or <see langword="null"/> while it runs.</summary>
    public int? ExitCode { get; private set; }

    /// <summary>Gets whether the program is running.</summary>
    public bool IsRunning => process is { HasExited: false };

    /// <inheritdoc/>
    public void Start(int columns, int rows)
    {
        if (process is not null)
            return;

        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.Environment["COLUMNS"] = columns.ToString();
        startInfo.Environment["LINES"] = rows.ToString();

        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("The program did not start.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Emit($"\u001b[31m{startInfo.FileName}: {exception.Message}\u001b[0m\r\n");
            Finish(127);

            return;
        }

        var reading = new[] { Pump(process.StandardOutput.BaseStream), Pump(process.StandardError.BaseStream) };

        _ = Task.Run(async () =>
        {
            await process.WaitForExitAsync();
            await Task.WhenAll(reading);

            Finish(process.ExitCode);
        });
    }

    /// <inheritdoc/>
    public void Send(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (process is not { HasExited: false } running)
            return;

        var edit = editor.Feed(text);

        if (Echo)
            Emit(edit.Output);

        foreach (var line in edit.Lines)
            Write(running, line + "\n");

        if (edit.Interrupted)
            Kill(running);

        if (edit.EndOfInput)
            Close(running);
    }

    /// <summary>The size cannot be passed on to a program that runs in a pipe, so this does nothing.</summary>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    public void Resize(int columns, int rows)
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        disposed = true;

        if (process is { HasExited: false } running)
            Kill(running);

        process?.Dispose();
    }

    /// <summary>Creates the start settings for a program.</summary>
    /// <param name="fileName">The program.</param>
    /// <param name="arguments">The arguments.</param>
    static ProcessStartInfo CreateStartInfo(string fileName, IEnumerable<string>? arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var info = new ProcessStartInfo(fileName);

        foreach (var argument in arguments ?? [])
            info.ArgumentList.Add(argument);

        return info;
    }

    /// <summary>Reads a stream of the program until it ends and gives it to the terminal.</summary>
    /// <param name="stream">The stream.</param>
    Task Pump(Stream stream) => Task.Run(async () =>
    {
        var buffer = new byte[4096];
        var afterReturn = false;
        int read;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            var translated = new List<byte>(read + 16);

            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n' && !afterReturn)
                    translated.Add((byte)'\r');

                translated.Add(buffer[i]);

                afterReturn = buffer[i] == (byte)'\r';
            }

            Emit(translated.ToArray());
        }
    });

    /// <summary>Sends text to the input of the program.</summary>
    /// <param name="running">The program.</param>
    /// <param name="text">The text.</param>
    static void Write(Process running, string text)
    {
        try
        {
            running.StandardInput.Write(text);
            running.StandardInput.Flush();
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Closes the input of the program.</summary>
    /// <param name="running">The program.</param>
    static void Close(Process running)
    {
        try
        {
            running.StandardInput.Close();
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Ends the program and its child processes.</summary>
    /// <param name="running">The program.</param>
    static void Kill(Process running)
    {
        try
        {
            running.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    /// <summary>Gives text to the terminal.</summary>
    /// <param name="text">The text.</param>
    void Emit(string text) => Emit(System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>Gives bytes to the terminal. Output is kept in order.</summary>
    /// <param name="data">The bytes.</param>
    void Emit(byte[] data)
    {
        if (data.Length == 0 || disposed)
            return;

        lock (gate)
            Output?.Invoke(this, new TerminalOutputEventArgs(data));
    }

    /// <summary>Reports the end of the program once.</summary>
    /// <param name="exitCode">The exit code.</param>
    void Finish(int exitCode)
    {
        if (ExitCode is not null)
            return;

        ExitCode = exitCode;

        if (!disposed)
            Exited?.Invoke(this, new TerminalExitedEventArgs(exitCode));
    }
}
