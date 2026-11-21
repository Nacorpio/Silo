using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Silo.Diagnostics;

/// <summary>
/// Connects to a <see cref="DiagnosticsServer"/> and sends it requests. The Inspector is built on it.
/// </summary>
/// <remarks>Requests are answered in order, so call it from one task at a time.</remarks>
/// <example>
/// <code language="csharp">
/// await using var client = await DiagnosticsClient.ConnectAsync(pipeName);
///
/// var tree = await client.CallAsync("tree.snapshot");
///
/// Console.WriteLine(tree.GetProperty("type").GetString());
/// </code>
/// </example>
public sealed class DiagnosticsClient : IAsyncDisposable
{
    /// <summary>The pipe to the server.</summary>
    readonly NamedPipeClientStream pipe;

    /// <summary>Reads the responses.</summary>
    readonly StreamReader reader;

    /// <summary>Writes the requests.</summary>
    readonly StreamWriter writer;

    /// <summary>The id of the last request.</summary>
    int lastId;

    /// <summary>Wraps a connected pipe.</summary>
    /// <param name="pipe">The connected pipe.</param>
    DiagnosticsClient(NamedPipeClientStream pipe)
    {
        this.pipe = pipe;

        reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>Connects to a server.</summary>
    /// <param name="pipeName">The pipe name, from <see cref="DiagnosticsServer.PipeName"/>.</param>
    /// <param name="timeout">How long to wait for the server to be listening.</param>
    /// <returns>The connected client.</returns>
    /// <exception cref="TimeoutException">No server was listening on the pipe in time.</exception>
    public static async Task<DiagnosticsClient> ConnectAsync(string pipeName, TimeSpan? timeout = null)
    {
        var options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, options);

        await pipe.ConnectAsync((int)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds);

        return new DiagnosticsClient(pipe);
    }

    /// <summary>Calls a method and waits for its result.</summary>
    /// <param name="method">The method name, such as <c>tree.snapshot</c>.</param>
    /// <param name="parameters">The <c>params</c> value as JSON text, or <see langword="null"/> for none.</param>
    /// <returns>The <c>result</c> of the response. The element stays valid for as long as the process lives.</returns>
    /// <exception cref="InvalidOperationException">The server answered with an error, or closed the connection.</exception>
    public async Task<JsonElement> CallAsync(string method, string? parameters = null)
    {
        var id = ++lastId;
        var methodText = JsonSerializer.Serialize(method, ClientJson.Default.String);
        var extra = parameters is null ? "" : $",\"params\":{parameters}";
        var request = $$"""{"jsonrpc":"2.0","id":{{id}},"method":{{methodText}}{{extra}}}""";

        await writer.WriteLineAsync(request);

        var line = await reader.ReadLineAsync()
            ?? throw new InvalidOperationException("The diagnostics server closed the connection.");
        var response = JsonDocument.Parse(line).RootElement;

        if (response.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.GetProperty("message").GetString());

        return response.GetProperty("result");
    }

    /// <summary>Closes the connection.</summary>
    /// <returns>A task that completes when the pipe is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        reader.Dispose();

        await writer.DisposeAsync();
        await pipe.DisposeAsync();
    }
}
