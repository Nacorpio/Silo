using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Opens a Control in the window's <see cref="OverlayLayer"/> right below an anchor Control, and keeps track of whether it is
/// still open. It is what the drop-down Controls, such as ComboBox and DatePicker, share.
/// </summary>
/// <remarks>
/// <para>
/// The popup sits at the left edge of the anchor and below it. The overlay layer slides it into the window when it would
/// stick out. It closes by itself on Escape or a press outside it, and <see cref="IsOpen"/> reflects that, so the anchor does
/// not have to listen for it.
/// </para>
/// <para>
/// The anchor creates the content, passes it to <see cref="Open"/> and keeps its own typed reference to it. The content is
/// not reused: create a new one for each <see cref="Open"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var popup = new AnchoredPopup(combo);
/// var list = new ListView { Items = ["Small", "Large"] };
///
/// popup.Open(list, matchWidth: true);
///
/// popup.Close();
/// </code>
/// </example>
/// <param name="anchor">The Control that the popup opens below. It must be arranged before <see cref="Open"/> is called.</param>
public sealed class AnchoredPopup(Control anchor)
{
    /// <summary>The Control that was passed to <see cref="Open"/> and not closed since, or <see langword="null"/>.</summary>
    Control? content;


    /// <summary>Gets whether the popup is open: it was opened, and the overlay layer still shows it.</summary>
    public bool IsOpen => content is not null && anchor.Overlays?.Contains(content) == true;

    /// <summary>Gets the content of the open popup, or <see langword="null"/> when it is closed.</summary>
    public Control? Content => IsOpen ? content : null;


    /// <summary>Finds where a Control sits when it opens below another one.</summary>
    /// <param name="anchor">The Control to open below.</param>
    /// <returns>The position of the popup's top left corner, relative to the window.</returns>
    public static (double X, double Y) Below(Control anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var (left, top) = anchor.ToLocal(0, 0);

        return (-left, -top + anchor.Bounds.Height);
    }

    /// <summary>Opens a Control below the anchor.</summary>
    /// <param name="popup">The Control to show. It must not be part of another tree.</param>
    /// <param name="matchWidth"><see langword="true"/> to give it the width of the anchor.</param>
    /// <returns>
    /// <see langword="true"/> when it opened. It does not when a popup is open already or the anchor is not in a window.
    /// </returns>
    /// <remarks>Call it after <see cref="Control.Arrange"/>, since the popup sits where the anchor was arranged.</remarks>
    public bool Open(Control popup, bool matchWidth = false)
    {
        ArgumentNullException.ThrowIfNull(popup);

        if (IsOpen || anchor.Overlays is not { } layer)
            return false;

        var (x, y) = Below(anchor);

        if (matchWidth)
            popup.SetValue(Control.WidthProperty, new Length(anchor.Bounds.Width, LengthUnit.Px));

        content = popup;

        layer.Show(popup, x, y);

        return true;
    }

    /// <summary>Closes the popup.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        var popup = content;

        content = null;

        return popup is not null && anchor.Overlays?.Close(popup) == true;
    }

    /// <summary>Gives a key to the open popup.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys that are held.</param>
    /// <returns><see langword="true"/> when the popup is open and handled the key.</returns>
    public bool KeyDown(Key key, KeyModifiers modifiers) => IsOpen && content!.KeyDown(key, modifiers);

    /// <summary>Gives typed text to the open popup.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the popup is open and took the text.</returns>
    public bool TextInput(string text) => IsOpen && content!.TextInput(text);
}
