namespace Silo;

/// <summary>
/// Keeps track of which Control has the keyboard focus and hands it the keys and the typed text of a platform window.
/// </summary>
/// <remarks>
/// <para>
/// A Control gets the focus when it is <see cref="Control.IsFocusable"/> and the pointer goes down on it or on one of its
/// descendants. Pressing anywhere else takes the focus away. The focused Control has the <c>focus</c> Pseudo-state.
/// </para>
/// <para>
/// Every method returns whether something changed. When it does, apply the Stylesheets again and render a new frame.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var focus = new FocusManager();
///
/// if (focus.FocusFrom(router.Hovered))
///     root.ApplyStyles(resolver);
///
/// focus.TextInput("a");
/// </code>
/// </example>
public sealed class FocusManager
{
    /// <summary>The Control that has the focus.</summary>
    Control? focused;

    /// <summary>Gets the Control that has the keyboard focus, or <see langword="null"/>.</summary>
    public Control? Focused => focused;

    /// <summary>Moves the focus to the nearest focusable Control at or above a Control.</summary>
    /// <param name="target">The Control that was pressed, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool FocusFrom(Control? target)
    {
        var next = target;

        while (next is not null && !next.IsFocusable)
            next = next.Parent as Control;

        return Focus(next);
    }

    /// <summary>Moves the focus to a Control, or removes it.</summary>
    /// <param name="control">The Control to focus, or <see langword="null"/> to clear the focus.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    public bool Focus(Control? control)
    {
        if (control == focused)
            return false;

        var changed = focused?.SetFocused(false) ?? false;

        focused = control;

        return (focused?.SetFocused(true) ?? false) | changed;
    }

    /// <summary>Sends a key press to the focused Control.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    public bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <summary>Sends a key press with its modifier keys to the focused Control.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys that were held down.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    public bool KeyDown(Key key, KeyModifiers modifiers) => focused?.KeyDown(key, modifiers) ?? false;

    /// <summary>Sends typed text to the focused Control.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the Control changed.</returns>
    public bool TextInput(string text) => focused?.TextInput(text) ?? false;
}
