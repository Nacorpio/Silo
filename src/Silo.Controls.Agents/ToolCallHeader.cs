using Silo.Layout;

namespace Silo;

/// <summary>
/// The row at the top of a <see cref="ToolCallCard"/>. Pressing it, or pressing Enter or the space bar while it has the focus, asks the card to expand or collapse.
/// </summary>
public sealed class ToolCallHeader : Box, IPointerHandler
{
    /// <summary>Occurs when the row is pressed or activated with the keyboard.</summary>
    public event EventHandler? Activated;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        Activated?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (key != Key.Enter)
            return false;

        Activated?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <inheritdoc/>
    public override bool TextInput(string text)
    {
        if (text != " ")
            return false;

        Activated?.Invoke(this, EventArgs.Empty);

        return true;
    }
}
