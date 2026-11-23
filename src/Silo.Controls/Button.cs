namespace Silo;

/// <summary>
/// A push button that shows a line of text and raises <see cref="Click"/> when it is pressed and released.
/// </summary>
/// <remarks>
/// <para>
/// A Button sets the <c>hover</c> Pseudo-state while the pointer is over it and the <c>active</c> Pseudo-state while it is
/// pressed, so a Stylesheet can restyle it with <c>Button:hover</c> and <c>Button:active</c>. The platform layer drives it
/// with <see cref="Control.SetHovered"/>, <see cref="PointerDown"/> and <see cref="PointerUp"/>.
/// </para>
/// <para>Styles must be applied again after the state changes to show the new look.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var save = new Button { Text = "Save" };
///
/// save.Click += (_, _) => document.Save();
/// </code>
/// </example>
public class Button : TextControl
{
    /// <summary>Raised when the pointer is released over the Button after it was pressed on it.</summary>
    public event EventHandler? Click;

    /// <summary>Presses the Button.</summary>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public virtual bool PointerDown() => SetPressed(true);

    /// <summary>Releases the Button.</summary>
    /// <param name="inside"><see langword="true"/> when the pointer is still over the Button, which makes the release a click.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public virtual bool PointerUp(bool inside)
    {
        var wasPressed = PseudoStates.Contains("active");

        var changed = SetPressed(false);

        if (!wasPressed || !inside)
            return changed;

        return changed | RaiseClick();
    }

    /// <summary>Runs <see cref="OnClick"/> and raises <see cref="Click"/>.</summary>
    /// <returns><see langword="true"/> when the Button changed a Pseudo-state, so styles need to be applied again.</returns>
    protected bool RaiseClick()
    {
        var changed = OnClick();

        Click?.Invoke(this, EventArgs.Empty);

        return changed;
    }

    /// <summary>Runs when the Button is clicked, before <see cref="Click"/> is raised. Override it to react to the click itself.</summary>
    /// <returns><see langword="true"/> when the Button changed a Pseudo-state, so styles need to be applied again. The default is <see langword="false"/>.</returns>
    protected virtual bool OnClick() => false;
}
