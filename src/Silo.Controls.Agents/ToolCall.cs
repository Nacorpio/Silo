using System.Text.Json;

namespace Silo;

/// <summary>
/// One call of a tool by an agent: what it was called with, how it is going and what it gave back.
/// </summary>
/// <remarks>
/// <para>
/// The same object is shown by a <see cref="ToolCallCard"/>, a <see cref="ToolCallTimeline"/> and whatever else the app builds on it.
/// Change it and the views follow, because it raises <see cref="Changed"/> for every change, including output that streams in with
/// <see cref="AppendOutput"/> and sub-calls added with <see cref="AddChild"/>.
/// </para>
/// <para>
/// A ToolCall is not thread safe. When the agent runs on another thread, change the call from the UI thread, for example through
/// <c>SdlWindow.Post</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var call = new ToolCall("read_file", """{ "path": "src/Program.cs" }""");
///
/// call.Start();
/// call.AppendOutput("using System;\n");
/// call.Succeed();
/// </code>
/// </example>
public sealed class ToolCall
{
    /// <summary>The sub-calls.</summary>
    readonly List<ToolCall> children = [];

    /// <summary>The arguments.</summary>
    string arguments;

    /// <summary>The summary that the app set.</summary>
    string summary = string.Empty;

    /// <summary>The status.</summary>
    ToolCallStatus status = ToolCallStatus.Pending;

    /// <summary>The output so far.</summary>
    string output = string.Empty;

    /// <summary>The error.</summary>
    string error = string.Empty;

    /// <summary>When the call started.</summary>
    DateTimeOffset? startedAt;

    /// <summary>When the call ended.</summary>
    DateTimeOffset? finishedAt;


    /// <summary>Creates a pending call.</summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="arguments">The arguments, usually as JSON.</param>
    public ToolCall(string name, string arguments = "")
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        this.arguments = arguments ?? string.Empty;
    }


    /// <summary>Occurs after any change to the call, and after a change to its list of sub-calls.</summary>
    public event EventHandler? Changed;


    /// <summary>Gets an identifier that is unique to this call.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Gets the name of the tool.</summary>
    public string Name { get; }

    /// <summary>Gets or sets the arguments, usually as JSON.</summary>
    public string Arguments
    {
        get => arguments;
        set => Set(ref arguments, value ?? string.Empty);
    }

    /// <summary>Gets or sets the one line that a collapsed card shows after the name. When empty, <see cref="DefaultSummary"/> is used.</summary>
    public string Summary
    {
        get => summary;
        set => Set(ref summary, value ?? string.Empty);
    }

    /// <summary>Gets or sets the status.</summary>
    public ToolCallStatus Status
    {
        get => status;
        set
        {
            if (value == status)
                return;

            status = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the output, which grows while the call runs.</summary>
    public string Output
    {
        get => output;
        set => Set(ref output, value ?? string.Empty);
    }

    /// <summary>Gets or sets the error text of a call that failed.</summary>
    public string Error
    {
        get => error;
        set => Set(ref error, value ?? string.Empty);
    }

    /// <summary>Gets or sets when the call started, or <see langword="null"/> while it has not.</summary>
    public DateTimeOffset? StartedAt
    {
        get => startedAt;
        set
        {
            if (value == startedAt)
                return;

            startedAt = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets when the call ended, or <see langword="null"/> while it has not.</summary>
    public DateTimeOffset? FinishedAt
    {
        get => finishedAt;
        set
        {
            if (value == finishedAt)
                return;

            finishedAt = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the sub-calls, for example the calls that a sub-agent made, in the order they were added.</summary>
    public IReadOnlyList<ToolCall> Children => children;

    /// <summary>Gets the summary of the call: <see cref="Summary"/>, or else the first text in the arguments.</summary>
    /// <remarks>For JSON arguments that is the first string value of the object; for other arguments, the first line.</remarks>
    public string DefaultSummary
    {
        get
        {
            if (summary.Length > 0)
                return summary;

            if (TryFirstString(arguments) is { } first)
                return first;

            var line = arguments.Trim().Split('\n', 2)[0].Trim();

            return line;
        }
    }


    /// <summary>Starts the call: it is running from now on.</summary>
    /// <param name="time">The clock that gives the start time. The default is the system clock.</param>
    public void Start(TimeProvider? time = null)
    {
        startedAt = (time ?? TimeProvider.System).GetUtcNow();
        finishedAt = null;
        status = ToolCallStatus.Running;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds text to the output.</summary>
    /// <param name="text">The text to append.</param>
    public void AppendOutput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0)
            Set(ref output, output + text);
    }

    /// <summary>Ends the call well.</summary>
    /// <param name="time">The clock that gives the end time. The default is the system clock.</param>
    public void Succeed(TimeProvider? time = null) => Finish(ToolCallStatus.Succeeded, time);

    /// <summary>Ends the call with an error.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="time">The clock that gives the end time. The default is the system clock.</param>
    public void Fail(string message, TimeProvider? time = null)
    {
        error = message ?? string.Empty;

        Finish(ToolCallStatus.Failed, time);
    }

    /// <summary>Ends the call because it was stopped.</summary>
    /// <param name="time">The clock that gives the end time. The default is the system clock.</param>
    public void Cancel(TimeProvider? time = null) => Finish(ToolCallStatus.Cancelled, time);

    /// <summary>Adds a sub-call.</summary>
    /// <param name="child">The call that was made on behalf of this one.</param>
    public void AddChild(ToolCall child)
    {
        ArgumentNullException.ThrowIfNull(child);

        children.Add(child);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets how long the call took, or has taken so far.</summary>
    /// <param name="time">The clock that gives the current time while the call is not finished.</param>
    /// <returns>The duration, or <see langword="null"/> when the call has not started.</returns>
    public TimeSpan? Elapsed(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return startedAt is { } start ? (finishedAt ?? time.GetUtcNow()) - start : null;
    }

    /// <summary>Ends the call.</summary>
    /// <param name="next">The status it ends in.</param>
    /// <param name="time">The clock that gives the end time.</param>
    void Finish(ToolCallStatus next, TimeProvider? time)
    {
        finishedAt = (time ?? TimeProvider.System).GetUtcNow();
        startedAt ??= finishedAt;
        status = next;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Changes a field and raises <see cref="Changed"/> when the value is another one.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The new value.</param>
    void Set(ref string field, string value)
    {
        if (field == value)
            return;

        field = value;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Finds the first string value in a JSON object.</summary>
    /// <param name="json">The text of the arguments.</param>
    /// <returns>The value, or <see langword="null"/> when the text is not a JSON object with a string in it.</returns>
    static string? TryFirstString(string json)
    {
        if (json.Length == 0 || json.TrimStart()[0] != '{')
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);

            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { Length: > 0 } text)
                    return text.Split('\n', 2)[0];
        }
        catch (JsonException)
        {
            // Half-streamed or plain text arguments have no first value.
        }

        return null;
    }
}
