using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// A <see cref="ToolCallCard"/> and, under it and indented, a group for each sub-call. A <see cref="ToolCallTimeline"/> is a column of these.
/// </summary>
/// <remarks>
/// The sub-calls are shown while the card is open, and a thin line down the left of them joins them to their card. A sub-call that
/// is added to the call later gets its group as soon as the call changes.
/// </remarks>
public sealed class ToolCallGroup : Control
{
    /// <summary>The card of the call.</summary>
    readonly ToolCallCard card = new();

    /// <summary>The indented column of the groups of the sub-calls.</summary>
    readonly Box childBox = new();

    /// <summary>The groups of the sub-calls, in the order of the calls.</summary>
    readonly List<ToolCallGroup> groups = [];

    /// <summary>The verbosity that the timeline gives.</summary>
    ToolCallVerbosity inherited = ToolCallVerbosity.Normal;


    /// <summary>Creates a group for a call and for the sub-calls that it has.</summary>
    /// <param name="call">The call.</param>
    public ToolCallGroup(ToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        Call = call;

        childBox.AddClass("children");

        card.Call = call;
        card.ExpandedChanged += (_, _) => RefreshChildren();
        card.CancelRequested += (_, c) => CancelRequested?.Invoke(this, c);
        card.RetryRequested += (_, c) => RetryRequested?.Invoke(this, c);

        call.Changed += (_, _) => Sync();

        Children.Add(card);

        Sync();
    }


    /// <summary>Occurs when the Cancel button of a card in this group or below it is pressed.</summary>
    public event EventHandler<ToolCall>? CancelRequested;

    /// <summary>Occurs when the Retry button of a card in this group or below it is pressed.</summary>
    public event EventHandler<ToolCall>? RetryRequested;


    /// <summary>Gets the call.</summary>
    public ToolCall Call { get; }

    /// <summary>Gets the card of the call.</summary>
    public ToolCallCard Card => card;

    /// <summary>Gets the groups of the sub-calls.</summary>
    public IReadOnlyList<ToolCallGroup> Groups => groups;

    /// <summary>Gets whether the sub-calls are shown: the card is open and there are some.</summary>
    public bool ShowsChildren => card.IsExpanded && groups.Count > 0;

    /// <summary>Gives the card, and the groups under it, the verbosity of the timeline.</summary>
    /// <param name="value">The verbosity.</param>
    internal void Inherit(ToolCallVerbosity value)
    {
        inherited = value;

        card.Inherit(value);

        foreach (var group in groups)
            group.Inherit(value);

        RefreshChildren();
    }

    /// <summary>Opens or closes this card and every card under it.</summary>
    /// <param name="value"><see langword="true"/> to open them.</param>
    internal void SetExpandedAll(bool value)
    {
        card.SetExpanded(value);

        foreach (var group in groups)
            group.SetExpandedAll(value);

        RefreshChildren();
    }

    /// <summary>Gives the clipboard and the time source to the cards.</summary>
    /// <param name="clipboard">The clipboard.</param>
    /// <param name="time">The time source.</param>
    internal void Configure(IClipboard? clipboard, TimeProvider time)
    {
        card.Clipboard = clipboard;
        card.TimeProvider = time;

        foreach (var group in groups)
            group.Configure(clipboard, time);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (!ShowsChildren)
            return;

        // A line from the card down the side of its sub-calls.
        var top = card.Bounds.Height;

        context.FillRect(new Rect(bounds.X + 9, bounds.Y + top, 1, Math.Max(0, bounds.Height - top - 3)), GetValue(BorderColorProperty));
    }

    /// <summary>Makes a group for each sub-call that has none yet.</summary>
    void Sync()
    {
        var added = false;

        foreach (var child in Call.Children.Skip(groups.Count))
        {
            var group = new ToolCallGroup(child);

            group.Inherit(inherited);
            group.Configure(card.Clipboard, card.TimeProvider);

            group.CancelRequested += (_, c) => CancelRequested?.Invoke(this, c);
            group.RetryRequested += (_, c) => RetryRequested?.Invoke(this, c);

            groups.Add(group);
            childBox.Children.Add(group);

            added = true;
        }

        if (added)
            RefreshChildren();
    }

    /// <summary>Puts the column of sub-calls in the tree while it is to be shown.</summary>
    void RefreshChildren()
    {
        if (Children.Contains(childBox) == ShowsChildren)
            return;

        if (ShowsChildren)
            Children.Add(childBox);
        else
            Children.Remove(childBox);
    }
}
