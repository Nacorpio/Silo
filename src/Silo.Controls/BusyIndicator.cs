using Silo.Layout;

namespace Silo;

/// <summary>
/// Wraps a Control and, while <see cref="IsBusy"/> is set, dims it, blocks the pointer from reaching it and shows a
/// <see cref="ProgressRing"/> with a message in the middle.
/// </summary>
/// <remarks>
/// <para>
/// Set the Control that is covered through <see cref="Content"/>. When <see cref="IsBusy"/> is <see langword="false"/> the
/// BusyIndicator is as big as its content and does nothing. When it is set, the content stays as it is and a layer with the
/// Class <c>busy-overlay</c> is laid over it: it holds the ring and the <see cref="Message"/> (shown when it is not empty), and
/// takes the pointer, so nothing under it can be pressed. The keyboard can still reach the content, so move the focus away when
/// the work starts if that matters.
/// </para>
/// <para>
/// Give the BusyIndicator a <c>width</c> and <c>height</c>, or a <c>flex-grow</c> in its parent, so that the layer has a
/// size that does not depend on the layer itself. Style the layer with <c>.busy-overlay</c> (the Default theme dims with the
/// <c>--scrim</c> token), the ring with <c>.busy-ring</c> and the text with <c>.busy-message</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var busy = new BusyIndicator { Content = table, Message = "Loading orders…" };
///
/// busy.IsBusy = true;
///
/// await LoadAsync();
///
/// busy.IsBusy = false;
/// </code>
/// </example>
public class BusyIndicator : Control
{
    /// <summary>The layer that dims the content.</summary>
    readonly Box overlay = new();

    /// <summary>The ring in the layer.</summary>
    readonly ProgressRing ring = new();

    /// <summary>The message in the layer.</summary>
    readonly TextBlock message = new();

    /// <summary>The Control that is covered.</summary>
    Control? content;

    /// <summary>Whether the layer is shown.</summary>
    bool isBusy;

    /// <summary>Creates a BusyIndicator that is not busy.</summary>
    public BusyIndicator()
    {
        overlay.AddClass("busy-overlay");
        ring.AddClass("busy-ring");
        message.AddClass("busy-message");

        overlay.Children.Add(ring);
    }

    /// <summary>Gets or sets the Control that is covered. Setting it replaces the previous content.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets whether the content is dimmed and blocked, and the ring is shown. The default is <see langword="false"/>.</summary>
    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (value == isBusy)
                return;

            isBusy = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the text under the ring. An empty text shows only the ring. The default is an empty text.</summary>
    public string Message
    {
        get => message.Text;
        set
        {
            message.Text = value ?? string.Empty;

            SyncMessage();
        }
    }

    /// <summary>Gets the layer that is shown over the content while the BusyIndicator is busy.</summary>
    public Control Overlay => overlay;

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect content)
    {
        foreach (var child in Children)
            PlaceChild((Control)child, content);

        return true;
    }

    /// <summary>Puts the content, and the layer while busy, in the tree.</summary>
    void Rebuild()
    {
        Children.Clear();

        if (content is not null)
            Children.Add(content);

        if (isBusy)
            Children.Add(overlay);
    }

    /// <summary>Shows the message in the layer when there is one.</summary>
    void SyncMessage()
    {
        var shown = overlay.Children.Contains(message);

        if (message.Text.Length > 0 && !shown)
            overlay.Children.Add(message);

        if (message.Text.Length == 0 && shown)
            overlay.Children.Remove(message);
    }
}
