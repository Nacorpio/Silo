using Silo.Rendering;

namespace Silo;

/// <summary>The corner of the window where a <see cref="ToastHost"/> shows its toasts.</summary>
public enum ToastPosition
{
    /// <summary>The top left corner.</summary>
    TopLeft,

    /// <summary>The top right corner.</summary>
    TopRight,

    /// <summary>The bottom left corner.</summary>
    BottomLeft,

    /// <summary>The bottom right corner. This is the default.</summary>
    BottomRight,
}

/// <summary>
/// Shows <see cref="Toast"/>s in a corner of the window and removes them after a while.
/// </summary>
/// <remarks>
/// <para>
/// A window has one ToastHost, which keeps its toasts in a column in the window's <see cref="OverlayLayer"/>, above the
/// content. The newest Toast is nearest to the corner, and the oldest is dismissed when there are more than
/// <see cref="MaxCount"/>. The pointer reaches a Toast, but a press beside it goes through to what is below.
/// </para>
/// <para>
/// A Toast is dismissed after <see cref="Duration"/>, unless that is zero. The time stops while the pointer is over the
/// Toast. Time is given to the host with <see cref="Tick"/>, which the window calls on every pass of its event loop, and
/// <see cref="DueIn"/> tells the window when the next Toast expires so that it wakes up for it.
/// </para>
/// <para>The theme styles the host (its <c>gap</c> and <c>padding</c> set the space between toasts and to the window edge) and the toasts.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// window.Toasts.Position = ToastPosition.TopRight;
///
/// window.Toasts.Show("Build finished", "CI", ToastSeverity.Success);
/// </code>
/// </example>
public class ToastHost : Control
{
    /// <summary>A position far enough away that the overlay layer moves the host to the corner.</summary>
    const double Far = 100_000;


    /// <summary>The layer the toasts are shown in.</summary>
    readonly OverlayLayer layer;

    /// <summary>The toasts that are shown, the oldest first.</summary>
    readonly List<Toast> toasts = [];


    /// <summary>The corner.</summary>
    ToastPosition position = ToastPosition.BottomRight;

    /// <summary>The largest number of toasts.</summary>
    int maxCount = 5;


    /// <summary>
    /// Runs before the first ToastHost is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ToastHost()
    {
    }

    /// <summary>Creates a ToastHost that shows its toasts in an overlay layer.</summary>
    /// <param name="layer">The layer of the window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="layer"/> is <see langword="null"/>.</exception>
    public ToastHost(OverlayLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        this.layer = layer;
    }

    /// <summary>Gets or sets the corner of the window. Toasts that are shown move to the new corner.</summary>
    public ToastPosition Position
    {
        get => position;
        set
        {
            if (value == position)
                return;

            position = value;

            Reshow();
        }
    }

    /// <summary>Gets or sets the largest number of toasts. When more are shown, the oldest are dismissed. The default is 5.</summary>
    public int MaxCount
    {
        get => maxCount;
        set
        {
            maxCount = Math.Max(1, value);

            Trim();
        }
    }

    /// <summary>Gets or sets how long a Toast stays when it has no duration of its own. <see cref="TimeSpan.Zero"/> keeps it until it is dismissed. The default is 4 seconds.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>Gets the toasts that are shown, the oldest first.</summary>
    public IReadOnlyList<Toast> Toasts => toasts;

    /// <summary>Shows a Toast.</summary>
    /// <param name="message">The message.</param>
    /// <param name="title">The title, or <see langword="null"/> for none.</param>
    /// <param name="severity">The kind.</param>
    /// <param name="icon">The icon, or <see langword="null"/> for none.</param>
    /// <param name="actionText">The text of an action button, or <see langword="null"/> for none.</param>
    /// <param name="duration">How long it stays, or <see langword="null"/> for <see cref="Duration"/>.</param>
    /// <returns>The Toast, so that its <see cref="Toast.ActionInvoked"/> and <see cref="Toast.Closed"/> can be handled.</returns>
    public Toast Show(
        string message,
        string? title = null,
        ToastSeverity severity = ToastSeverity.Info,
        IImage? icon = null,
        string? actionText = null,
        TimeSpan? duration = null)
    {
        var toast = new Toast
        {
            Message = message,
            Title = title ?? string.Empty,
            Severity = severity,
            Icon = icon,
            ActionText = actionText ?? string.Empty,
            Duration = duration,
        };

        toast.CloseRequested += (_, _) => Dismiss(toast);

        toast.Remaining = LifetimeOf(toast);

        toasts.Add(toast);

        Trim();
        Reshow();

        return toast;
    }

    /// <summary>Removes a Toast.</summary>
    /// <param name="toast">The Toast.</param>
    /// <returns><see langword="true"/> when it was shown.</returns>
    public bool Dismiss(Toast toast)
    {
        if (!toasts.Remove(toast))
            return false;

        toast.RaiseClosed();

        Reshow();

        return true;
    }

    /// <summary>Removes every Toast.</summary>
    /// <returns><see langword="true"/> when any was shown.</returns>
    public bool DismissAll()
    {
        var all = toasts.ToArray();

        toasts.Clear();

        foreach (var toast in all)
            toast.RaiseClosed();

        Reshow();

        return all.Length > 0;
    }

    /// <summary>Counts time for the toasts and dismisses the ones that ran out.</summary>
    /// <param name="now">The time since some fixed start, such as the start of the window. It only needs to be steady.</param>
    /// <returns><see langword="true"/> when a Toast was dismissed, so a new frame is needed.</returns>
    /// <remarks>The time of a Toast stands still while the pointer is over it.</remarks>
    public bool Tick(TimeSpan now)
    {
        var expired = new List<Toast>();

        foreach (var toast in toasts)
        {
            var elapsed = now - (toast.Counted ?? now);

            toast.Counted = now;

            if (toast.Remaining is not { } remaining || toast.IsHovered)
                continue;

            toast.Remaining = remaining - elapsed;

            if (toast.Remaining <= TimeSpan.Zero)
                expired.Add(toast);
        }

        foreach (var toast in expired)
            Dismiss(toast);

        return expired.Count > 0;
    }

    /// <summary>Tells when the next Toast runs out, so that the window can wake up for it.</summary>
    /// <returns>The time from the last <see cref="Tick"/>, or <see langword="null"/> when no Toast is counting down.</returns>
    public TimeSpan? DueIn()
    {
        var due = toasts
            .Where(t => t.Remaining is not null && !t.IsHovered)
            .Select(t => t.Remaining!.Value)
            .ToList();

        return due.Count == 0 ? null : due.Min() is { Ticks: > 0 } left ? left : TimeSpan.Zero;
    }

    /// <summary>Gets how long a Toast stays from now on.</summary>
    /// <param name="toast">The Toast.</param>
    /// <returns>The time, or <see langword="null"/> when it stays until it is dismissed.</returns>
    TimeSpan? LifetimeOf(Toast toast)
        => (toast.Duration ?? Duration) is { Ticks: > 0 } time ? time : null;

    /// <summary>Dismisses the oldest toasts while there are too many.</summary>
    void Trim()
    {
        while (toasts.Count > maxCount)
            Dismiss(toasts[0]);
    }

    /// <summary>Puts the toasts in the column in the order of the corner, and shows or hides the column.</summary>
    void Reshow()
    {
        var onTop = position is ToastPosition.TopLeft or ToastPosition.TopRight;
        var ordered = onTop ? toasts.AsEnumerable().Reverse() : toasts;

        layer.Close(this);

        Children.Clear();

        foreach (var toast in ordered)
            Children.Add(toast);

        if (toasts.Count == 0)
            return;

        var right = position is ToastPosition.TopRight or ToastPosition.BottomRight;

        layer.Show(this, right ? Far : 0, onTop ? 0 : Far, modal: false, interactive: true);
    }
}
