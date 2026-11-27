using System.Globalization;
using System.Text.Json;

namespace Silo;

/// <summary>
/// One <see cref="ToolCall"/>: a row with its status, name, summary and duration, that opens to the arguments and the output.
/// </summary>
/// <remarks>
/// <para>
/// What the card shows is set by <see cref="Verbosity"/>. <see cref="ToolCallVerbosity.Normal"/> is one row that opens on a press, and a
/// call that fails opens itself once, unless the card was opened or closed by hand before. A card in a <see cref="ToolCallTimeline"/>
/// follows the timeline's verbosity until its own is set.
/// </para>
/// <para>
/// The status gives the card the class <c>status-</c> plus <see cref="ToolCallStatus.Name"/> and, from its kind, the states
/// <c>:waiting</c>, <c>:running</c>, <c>:succeeded</c> or <c>:failed</c>. A running call shows a spinner and a duration that ticks. The
/// card never cancels or retries a call itself: it raises <see cref="CancelRequested"/> and <see cref="RetryRequested"/> with the call.
/// </para>
/// <para>
/// The look comes from the theme through Classes on the parts: <c>header</c>, <c>chevron</c>, <c>status</c>, <c>name</c>, <c>summary</c>,
/// <c>duration</c>, <c>action</c> (with <c>cancel</c> or <c>retry</c>), <c>body</c>, <c>label</c>, <c>arguments</c>, <c>output</c>,
/// <c>error</c>, <c>copy</c> and <c>meta</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var call = new ToolCall("run_tests", """{ "filter": "Silo.Controls" }""");
/// var card = new ToolCallCard { Call = call };
///
/// card.CancelRequested += (_, c) => agent.Cancel(c.Id);
///
/// call.Start();
/// </code>
/// </example>
[PseudoStates("expanded", "waiting", "running", "succeeded", "failed")]
public class ToolCallCard : Control
{
    /// <summary>The format of a duration of a second or more: one decimal at most.</summary>
    const string DurationFormat = "0.#";

    /// <summary>The row at the top.</summary>
    readonly ToolCallHeader header = new();

    /// <summary>The arrow in front of the row.</summary>
    readonly Icon chevron = new();

    /// <summary>The icon of the status.</summary>
    readonly Icon statusIcon = new();

    /// <summary>The spinner that replaces the icon while the call runs.</summary>
    readonly ProgressRing ring = new();

    /// <summary>The name of the tool.</summary>
    readonly TextBlock name = new();

    /// <summary>The summary of the arguments.</summary>
    readonly ToolCallSummary summary = new();

    /// <summary>How long the call took.</summary>
    readonly TextBlock duration = new();

    /// <summary>Stops a running call.</summary>
    readonly Button cancel = new() { Text = "Cancel" };

    /// <summary>Runs a failed call again.</summary>
    readonly Button retry = new() { Text = "Retry" };

    /// <summary>The part below the row.</summary>
    readonly Box body = new();

    /// <summary>The label above the arguments.</summary>
    readonly TextBlock argumentsLabel = new() { Text = "Arguments" };

    /// <summary>Shows the arguments.</summary>
    readonly TextArea arguments = new() { IsReadOnly = true };

    /// <summary>The label above the output.</summary>
    readonly TextBlock outputLabel = new() { Text = "Output" };

    /// <summary>Shows the output.</summary>
    readonly TextArea output = new() { IsReadOnly = true };

    /// <summary>The error text.</summary>
    readonly TextBlock error = new();

    /// <summary>Copies the output.</summary>
    readonly Button copy = new() { Text = "Copy output" };

    /// <summary>The id, times and status that the debug verbosity adds.</summary>
    readonly TextBlock meta = new();

    /// <summary>The call that the card shows.</summary>
    ToolCall? call;

    /// <summary>The verbosity that was set on the card, or <see langword="null"/> to follow the timeline.</summary>
    ToolCallVerbosity? verbosity;

    /// <summary>The verbosity that the timeline gives.</summary>
    ToolCallVerbosity inherited = ToolCallVerbosity.Normal;

    /// <summary>The density that was set on the card, or <see langword="null"/> to follow the timeline.</summary>
    ToolCallDensity? density;

    /// <summary>The density that the timeline gives.</summary>
    ToolCallDensity inheritedDensity = ToolCallDensity.Normal;

    /// <summary>Whether the card is expanded, as far as <see cref="ToolCallVerbosity.Normal"/> goes.</summary>
    bool expanded;

    /// <summary>Whether a person opened or closed the card, which the card then keeps.</summary>
    bool toggledByHand;

    /// <summary>The status the card was last built for, so that it opens itself only when a call turns into a failure.</summary>
    ToolCallStatus shownStatus = ToolCallStatus.Pending;

    /// <summary>The class that the status gave the card.</summary>
    string statusClass = string.Empty;

    /// <summary>The time source for the duration of a running call.</summary>
    TimeProvider time = TimeProvider.System;


    static ToolCallCard()
    {
    }

    /// <summary>Creates a card with no call.</summary>
    public ToolCallCard()
    {
        header.AddClass("header");
        chevron.AddClass("chevron");
        statusIcon.AddClass("status");
        ring.AddClass("status");
        name.AddClass("name");
        summary.AddClass("summary");
        duration.AddClass("duration");
        cancel.AddClass("action");
        cancel.AddClass("cancel");
        retry.AddClass("action");
        retry.AddClass("retry");
        body.AddClass("body");
        argumentsLabel.AddClass("label");
        outputLabel.AddClass("label");
        arguments.AddClass("arguments");
        output.AddClass("output");
        error.AddClass("error");
        copy.AddClass("copy");
        meta.AddClass("meta");

        header.Activated += (_, _) => ToggleByHand();
        cancel.Click += (_, _) => OnCancel();
        retry.Click += (_, _) => OnRetry();
        copy.Click += (_, _) => Clipboard?.SetText(call?.Output ?? string.Empty);

        Children.Add(header);

        Rebuild();
    }


    /// <summary>Occurs when the Cancel button of a running call is pressed. The argument is the call.</summary>
    public event EventHandler<ToolCall>? CancelRequested;

    /// <summary>Occurs when the Retry button of a failed call is pressed. The argument is the call.</summary>
    public event EventHandler<ToolCall>? RetryRequested;

    /// <summary>Occurs when the card opened or closed.</summary>
    public event EventHandler? ExpandedChanged;


    /// <summary>Gets or sets the call that the card shows.</summary>
    public ToolCall? Call
    {
        get => call;
        set
        {
            if (call == value)
                return;

            if (call is not null)
                call.Changed -= OnCallChanged;

            call = value;
            toggledByHand = false;
            expanded = false;
            shownStatus = ToolCallStatus.Pending;

            if (call is not null)
            {
                call.Changed += OnCallChanged;

                expanded = call.Status.Kind == ToolCallStatusKind.Failure;
                shownStatus = call.Status;
            }

            Rebuild();
        }
    }

    /// <summary>
    /// Gets or sets how much of the call is shown. Until it is set, a card follows the <see cref="ToolCallTimeline"/> it is in, and
    /// is <see cref="ToolCallVerbosity.Normal"/> on its own.
    /// </summary>
    public ToolCallVerbosity Verbosity
    {
        get => verbosity ?? inherited;
        set
        {
            verbosity = value;

            Rebuild();
        }
    }

    /// <summary>
    /// Gets or sets how much room the card leaves around its content. Until it is set, a card follows the <see cref="ToolCallTimeline"/>
    /// it is in, and is <see cref="ToolCallDensity.Normal"/> on its own.
    /// </summary>
    public ToolCallDensity Density
    {
        get => density ?? inheritedDensity;
        set
        {
            density = value;

            ApplyDensity();
        }
    }

    /// <summary>Makes the card follow the timeline's density again.</summary>
    public void ClearDensity()
    {
        density = null;

        ApplyDensity();
    }

    /// <summary>Makes the card follow the timeline's verbosity again.</summary>
    public void ClearVerbosity()
    {
        verbosity = null;

        Rebuild();
    }

    /// <summary>Gets whether the body is shown: always for the detailed verbosities, never for the minimal one, else as the card was opened.</summary>
    public bool IsExpanded => Verbosity switch
    {
        ToolCallVerbosity.Minimal => false,
        ToolCallVerbosity.Normal => expanded,

        _ => true,
    };

    /// <summary>Gets or sets the clipboard that Copy output writes to.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>Gets or sets the time source for the duration of a running call. The default is the system clock.</summary>
    public TimeProvider TimeProvider
    {
        get => time;
        set => time = value ?? TimeProvider.System;
    }

    /// <summary>Gets the row at the top, which takes the focus.</summary>
    public ToolCallHeader Header => header;

    /// <inheritdoc/>
    public override bool IsAnimating => call?.Status.Kind == ToolCallStatusKind.Active;

    /// <summary>Opens the card, as far as the verbosity allows it.</summary>
    /// <param name="value"><see langword="true"/> to open it, <see langword="false"/> to close it.</param>
    public void SetExpanded(bool value)
    {
        toggledByHand = true;

        Expand(value);
    }

    /// <summary>Sets the density that a timeline gives, which counts until the card has its own.</summary>
    /// <param name="value">The density.</param>
    internal void InheritDensity(ToolCallDensity value)
    {
        inheritedDensity = value;

        ApplyDensity();
    }

    /// <summary>Puts the Class of the density on the card.</summary>
    void ApplyDensity()
    {
        _ = RemoveClass("density-compact");
        _ = RemoveClass("density-roomy");

        _ = Density switch
        {
            ToolCallDensity.Compact => AddClass("density-compact"),
            ToolCallDensity.Roomy => AddClass("density-roomy"),
            _ => false,
        };
    }

    /// <summary>Sets the verbosity that a timeline gives, which counts until the card has its own.</summary>
    /// <param name="value">The verbosity.</param>
    internal void Inherit(ToolCallVerbosity value)
    {
        inherited = value;

        if (verbosity is null)
            Rebuild();
    }

    /// <inheritdoc/>
    protected override bool OnFrame()
    {
        RefreshDuration();

        return IsAnimating;
    }

    /// <inheritdoc/>
    protected override void OnRender(Silo.Rendering.IDrawingContext context, Silo.Layout.Rect bounds)
    {
        // The duration of a running call is read from the clock, so that it is right whenever a frame is drawn.
        RefreshDuration();

        base.OnRender(context, bounds);
    }

    /// <summary>Formats a duration for the row.</summary>
    /// <param name="value">The duration.</param>
    /// <returns>For example <c>"340 ms"</c>, <c>"1.2 s"</c> or <c>"2m 05s"</c>.</returns>
    public static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.FromSeconds(1))
            return $"{Math.Max(0, (int)value.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)} ms";

        if (value < TimeSpan.FromMinutes(1))
            return $"{value.TotalSeconds.ToString(DurationFormat, CultureInfo.InvariantCulture)} s";

        return $"{(int)value.TotalMinutes}m {value.Seconds:00}s";
    }

    /// <summary>Pretty prints JSON, and returns other text as it is.</summary>
    /// <param name="text">The arguments.</param>
    /// <returns>The text to show.</returns>
    static string Pretty(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
            return text;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                document.WriteTo(writer);

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            // Arguments that are still streaming in are not JSON yet.
            return text;
        }
    }

    /// <summary>Reacts to a change of the call.</summary>
    /// <param name="sender">The call.</param>
    /// <param name="e">Nothing.</param>
    void OnCallChanged(object? sender, EventArgs e)
    {
        if (call is null)
            return;

        // A call that turns into a failure opens its card once, unless a person already chose.
        if (call.Status != shownStatus && call.Status.Kind == ToolCallStatusKind.Failure && !toggledByHand && Verbosity == ToolCallVerbosity.Normal)
            expanded = true;

        shownStatus = call.Status;

        Rebuild();
    }

    /// <summary>Opens or closes the card because a person pressed the row.</summary>
    void ToggleByHand()
    {
        if (Verbosity != ToolCallVerbosity.Normal)
            return;

        toggledByHand = true;

        Expand(!expanded);
    }

    /// <summary>Changes the expanded state of the Normal verbosity.</summary>
    /// <param name="value">The new state.</param>
    void Expand(bool value)
    {
        if (expanded == value)
            return;

        var before = IsExpanded;

        expanded = value;

        Rebuild();

        if (before != IsExpanded)
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises <see cref="CancelRequested"/>.</summary>
    void OnCancel()
    {
        if (call is not null)
            CancelRequested?.Invoke(this, call);
    }

    /// <summary>Raises <see cref="RetryRequested"/>.</summary>
    void OnRetry()
    {
        if (call is not null)
            RetryRequested?.Invoke(this, call);
    }

    /// <summary>Writes the call into the parts and puts the parts that the verbosity shows in the tree.</summary>
    void Rebuild()
    {
        var status = call?.Status ?? ToolCallStatus.Pending;
        var level = Verbosity;

        RefreshStatusClass(status);

        SetPseudoState("expanded", IsExpanded);
        SetPseudoState("waiting", status.Kind == ToolCallStatusKind.Waiting);
        SetPseudoState("running", status.Kind == ToolCallStatusKind.Active);
        SetPseudoState("succeeded", status.Kind == ToolCallStatusKind.Success);
        SetPseudoState("failed", status.Kind == ToolCallStatusKind.Failure);

        name.Text = call?.Name ?? string.Empty;
        summary.Text = call?.DefaultSummary ?? string.Empty;

        statusIcon.Glyph = status.Kind switch
        {
            ToolCallStatusKind.Success => "check",
            ToolCallStatusKind.Failure => "error",
            ToolCallStatusKind.Waiting => "question",
            _ => "minus",
        };

        chevron.Glyph = IsExpanded ? "chevron-down" : "chevron-right";

        header.Children.Clear();

        if (level != ToolCallVerbosity.Minimal)
            header.Children.Add(chevron);

        header.Children.Add(status.Kind == ToolCallStatusKind.Active ? ring : statusIcon);
        header.Children.Add(name);

        if (level != ToolCallVerbosity.Minimal)
        {
            header.Children.Add(summary);
            header.Children.Add(duration);

            if (status.Kind == ToolCallStatusKind.Active)
                header.Children.Add(cancel);
            else if (status.Kind == ToolCallStatusKind.Failure)
                header.Children.Add(retry);
        }

        RefreshDuration();
        RebuildBody(status, level);

        if (Children.Contains(body) != IsExpanded)
        {
            if (IsExpanded)
                Children.Add(body);
            else
                Children.Remove(body);
        }
    }

    /// <summary>Gives the card the class of its status and takes the one of the last status away.</summary>
    /// <param name="status">The status.</param>
    void RefreshStatusClass(ToolCallStatus status)
    {
        var next = $"status-{status.Name}";

        if (next == statusClass)
            return;

        if (statusClass.Length > 0)
            RemoveClass(statusClass);

        AddClass(next);

        statusClass = next;
    }

    /// <summary>Puts the sections of the body in place and fills them.</summary>
    /// <param name="status">The status of the call.</param>
    /// <param name="level">The verbosity.</param>
    void RebuildBody(ToolCallStatus status, ToolCallVerbosity level)
    {
        body.Children.Clear();

        if (call is null || !IsExpanded)
            return;

        SetText(arguments, Pretty(call.Arguments));
        SetText(output, call.Output);

        if (call.Arguments.Length > 0)
        {
            body.Children.Add(argumentsLabel);
            body.Children.Add(arguments);
        }

        if (call.Output.Length > 0)
        {
            body.Children.Add(outputLabel);
            body.Children.Add(output);
        }

        if (status.Kind == ToolCallStatusKind.Failure && call.Error.Length > 0)
        {
            error.Text = call.Error;

            body.Children.Add(error);
        }
        else if (status.Kind == ToolCallStatusKind.Waiting)
        {
            error.Text = status.Label;

            body.Children.Add(error);
        }

        if (call.Output.Length > 0)
            body.Children.Add(copy);

        if (level == ToolCallVerbosity.Debug)
        {
            meta.Text = $"{call.Id} · {status.Name} · {Describe(call.StartedAt)} → {Describe(call.FinishedAt)}";

            body.Children.Add(meta);
        }
    }

    /// <summary>Puts text in a read-only area and follows the end, as streaming output does.</summary>
    /// <param name="area">The area.</param>
    /// <param name="text">The text.</param>
    static void SetText(TextArea area, string text)
    {
        if (area.Buffer.Text == text)
            return;

        area.Buffer.Reset(text);
        area.Buffer.MoveToEnd(false);
    }

    /// <summary>Formats a time for the debug line.</summary>
    /// <param name="value">The time, or <see langword="null"/>.</param>
    /// <returns>The time of day, or a dash.</returns>
    static string Describe(DateTimeOffset? value)
        => value is { } v ? v.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) : "–";

    /// <summary>Writes the duration into its label.</summary>
    void RefreshDuration()
    {
        duration.Text = call?.Elapsed(time) is { } elapsed ? FormatDuration(elapsed) : string.Empty;
    }
}
