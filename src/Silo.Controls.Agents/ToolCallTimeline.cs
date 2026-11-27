namespace Silo;

/// <summary>
/// The tool calls of one turn, as a column of <see cref="ToolCallCard"/>s, in the order they started. The calls that a call made
/// are indented under it.
/// </summary>
/// <remarks>
/// <para>
/// A line at the top counts the calls, such as <c>12 tool calls · 2 running · 1 failed</c>, sub-calls included. Parallel calls are
/// several running cards. <see cref="Verbosity"/> is given to every card that has not set its own, and <see cref="ExpandAll"/>
/// and <see cref="CollapseAll"/> open and close them all.
/// </para>
/// <para>
/// The timeline does not scroll and is not virtualized: put it in a <see cref="ScrollViewer"/>, or in whatever scrolls the
/// conversation. That is fine for hundreds of calls. A chart of calls on a time axis is a different Control.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var timeline = new ToolCallTimeline { Verbosity = ToolCallVerbosity.Normal };
///
/// timeline.Add(readFile);
/// timeline.Add(runTests);
/// timeline.CancelRequested += (_, call) => agent.Cancel(call.Id);
/// </code>
/// </example>
public class ToolCallTimeline : Control
{
    /// <summary>The line with the counts.</summary>
    readonly TextBlock summary = new();

    /// <summary>The groups of the calls that were added, in order.</summary>
    readonly List<ToolCallGroup> groups = [];

    /// <summary>The calls that are watched for changes.</summary>
    readonly HashSet<ToolCall> watched = [];

    /// <summary>The calls that were added.</summary>
    readonly List<ToolCall> calls = [];

    /// <summary>The verbosity.</summary>
    ToolCallVerbosity verbosity = ToolCallVerbosity.Normal;

    /// <summary>The density.</summary>
    ToolCallDensity density = ToolCallDensity.Normal;

    /// <summary>The clipboard.</summary>
    IClipboard? clipboard;

    /// <summary>The time source.</summary>
    TimeProvider time = TimeProvider.System;


    static ToolCallTimeline()
    {
    }

    /// <summary>Creates an empty timeline.</summary>
    public ToolCallTimeline()
    {
        summary.AddClass("summary");

        Children.Add(summary);

        RefreshSummary();
    }


    /// <summary>Occurs when the Cancel button of a running call is pressed.</summary>
    public event EventHandler<ToolCall>? CancelRequested;

    /// <summary>Occurs when the Retry button of a failed call is pressed.</summary>
    public event EventHandler<ToolCall>? RetryRequested;


    /// <summary>Gets the calls that were added, not counting sub-calls.</summary>
    public IReadOnlyList<ToolCall> Calls => calls;

    /// <summary>Gets the groups, one for each call that was added.</summary>
    public IReadOnlyList<ToolCallGroup> Groups => groups;

    /// <summary>Gets the line with the counts.</summary>
    public string Summary => summary.Text;

    /// <summary>Gets or sets the verbosity of the cards that have not set their own. The default is <see cref="ToolCallVerbosity.Normal"/>.</summary>
    public ToolCallVerbosity Verbosity
    {
        get => verbosity;
        set
        {
            verbosity = value;

            foreach (var group in groups)
                group.Inherit(value);
        }
    }

    /// <summary>
    /// Gets or sets how much room the timeline and the cards that have not set their own density leave. The default is
    /// <see cref="ToolCallDensity.Normal"/>. The theme sees it as the Classes <c>density-compact</c> and <c>density-roomy</c>.
    /// </summary>
    public ToolCallDensity Density
    {
        get => density;
        set
        {
            density = value;

            _ = RemoveClass("density-compact");
            _ = RemoveClass("density-roomy");

            _ = value switch
            {
                ToolCallDensity.Compact => AddClass("density-compact"),
                ToolCallDensity.Roomy => AddClass("density-roomy"),
                _ => false,
            };

            foreach (var group in groups)
                group.InheritDensity(value);
        }
    }

    /// <summary>Gets or sets the clipboard that the Copy buttons of the cards write to.</summary>
    public IClipboard? Clipboard
    {
        get => clipboard;
        set
        {
            clipboard = value;

            Configure();
        }
    }

    /// <summary>Gets or sets the time source for the duration of running calls. The default is the system clock.</summary>
    public TimeProvider TimeProvider
    {
        get => time;
        set
        {
            time = value ?? TimeProvider.System;

            Configure();
        }
    }


    /// <summary>Adds a call at the end.</summary>
    /// <param name="call">The call. Its sub-calls are shown under it, also the ones that are added later.</param>
    public void Add(ToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        var group = new ToolCallGroup(call);

        group.Inherit(verbosity);
        group.InheritDensity(density);
        group.Configure(clipboard, time);

        group.CancelRequested += (_, c) => CancelRequested?.Invoke(this, c);
        group.RetryRequested += (_, c) => RetryRequested?.Invoke(this, c);

        calls.Add(call);
        groups.Add(group);
        Children.Add(group);

        Watch(call);
        RefreshSummary();
    }

    /// <summary>Removes every call.</summary>
    public void Clear()
    {
        foreach (var group in groups)
            Children.Remove(group);

        foreach (var call in watched)
            call.Changed -= OnCallChanged;

        groups.Clear();
        calls.Clear();
        watched.Clear();

        RefreshSummary();
    }

    /// <summary>Opens every card, including the ones of sub-calls.</summary>
    public void ExpandAll()
    {
        foreach (var group in groups)
            group.SetExpandedAll(true);
    }

    /// <summary>Closes every card, including the ones of sub-calls.</summary>
    public void CollapseAll()
    {
        foreach (var group in groups)
            group.SetExpandedAll(false);
    }

    /// <summary>Counts a call and its sub-calls.</summary>
    /// <param name="call">The call.</param>
    /// <param name="total">The number of calls.</param>
    /// <param name="running">The number that are running.</param>
    /// <param name="failed">The number that failed.</param>
    static void Count(ToolCall call, ref int total, ref int running, ref int failed)
    {
        total++;

        if (call.Status.Kind == ToolCallStatusKind.Active)
            running++;

        if (call.Status.Kind == ToolCallStatusKind.Failure)
            failed++;

        foreach (var child in call.Children)
            Count(child, ref total, ref running, ref failed);
    }

    /// <summary>Gives the clipboard and the time source to the groups.</summary>
    void Configure()
    {
        foreach (var group in groups)
            group.Configure(clipboard, time);
    }

    /// <summary>Starts to watch a call and its sub-calls, so that the counts follow them.</summary>
    /// <param name="call">The call.</param>
    void Watch(ToolCall call)
    {
        if (!watched.Add(call))
            return;

        call.Changed += OnCallChanged;

        foreach (var child in call.Children)
            Watch(child);
    }

    /// <summary>Updates the counts after a call changed, and watches sub-calls that came with the change.</summary>
    /// <param name="sender">The call.</param>
    /// <param name="e">Nothing.</param>
    void OnCallChanged(object? sender, EventArgs e)
    {
        if (sender is ToolCall call)
            foreach (var child in call.Children)
                Watch(child);

        RefreshSummary();
    }

    /// <summary>Writes the counts into the line at the top.</summary>
    void RefreshSummary()
    {
        int total = 0, running = 0, failed = 0;

        foreach (var call in calls)
            Count(call, ref total, ref running, ref failed);

        var parts = new List<string> { total == 0 ? "No tool calls" : total == 1 ? "1 tool call" : $"{total} tool calls" };

        if (running > 0)
            parts.Add($"{running} running");

        if (failed > 0)
            parts.Add($"{failed} failed");

        summary.Text = string.Join(" · ", parts);
    }
}
