using System.IO.Pipes;
using System.Text;

namespace Silo.Diagnostics;

/// <summary>
/// Serves a <see cref="DiagnosticsAgent"/> over a local named pipe, so that tools in other processes can inspect the app.
/// </summary>
/// <remarks>
/// <para>
/// The transport is newline-delimited JSON: each request and each response is one line of JSON-RPC text. The pipe is
/// restricted to the current operating-system user, and it never listens on a network port.
/// </para>
/// <para>
/// Requests arrive on thread-pool threads, but the agent reads the live Element tree. The server therefore never calls the
/// agent itself: it hands each request to the <c>invoke</c> function from the constructor, which must run the work on the UI
/// thread and return its result.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// using var server = new DiagnosticsServer(agent, work =>
/// {
///     var done = new TaskCompletionSource&lt;string?&gt;();
///
///     window.Post(() => done.SetResult(work()));
///
///     return done.Task;
/// });
///
/// server.Start();
///
/// Console.WriteLine($"Inspect me on pipe {server.PipeName}");
/// </code>
/// </example>
public sealed class DiagnosticsServer : IDisposable
{
    /// <summary>The agent that answers requests.</summary>
    readonly DiagnosticsAgent agent;

    /// <summary>Runs a piece of work on the UI thread.</summary>
    readonly Func<Func<string?>, Task<string?>> invoke;

    /// <summary>Stops the accept loop and the open connections.</summary>
    readonly CancellationTokenSource stop = new();

    /// <summary>Creates a server. Call <see cref="Start"/> to begin listening.</summary>
    /// <param name="agent">The agent that answers requests.</param>
    /// <param name="invoke">Runs the given work on the UI thread and returns what it returned.</param>
    /// <param name="pipeName">The pipe name, or <see langword="null"/> for <c>silo-diagnostics-</c> followed by the process id.</param>
    public DiagnosticsServer(DiagnosticsAgent agent, Func<Func<string?>, Task<string?>> invoke, string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(invoke);

        this.agent = agent;
        this.invoke = invoke;

        PipeName = pipeName ?? $"silo-diagnostics-{Environment.ProcessId}";
    }

    /// <summary>Gets the name of the pipe that clients connect to.</summary>
    public string PipeName { get; }

    /// <summary>Starts accepting connections in the background.</summary>
    public void Start() => _ = Task.Run(AcceptAsync);

    /// <summary>Stops listening and closes the connections.</summary>
    public void Dispose() => stop.Cancel();

    /// <summary>Accepts connections one after another until the server is disposed.</summary>
    async Task AcceptAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await pipe.WaitForConnectionAsync(stop.Token);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();

                return;
            }

            _ = Task.Run(() => ServeAsync(pipe));
        }
    }

    /// <summary>Answers the requests of one connection until it closes.</summary>
    /// <param name="pipe">The connected pipe, which is disposed at the end.</param>
    async Task ServeAsync(NamedPipeServerStream pipe)
    {
        await using (pipe)
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

            try
            {
                while (await reader.ReadLineAsync(stop.Token) is { } line)
                    if (await invoke(() => agent.Handle(line)) is { } reply)
                        await writer.WriteLineAsync(reply.AsMemory(), stop.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // The client went away or the server stopped, which ends this connection and nothing else.
            }
        }
    }
}
