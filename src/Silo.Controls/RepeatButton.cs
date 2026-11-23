namespace Silo;

/// <summary>
/// A button that raises <see cref="Button.Click"/> again and again while it is held down, such as the arrows of a scroll bar or a
/// stepper.
/// </summary>
/// <remarks>
/// <para>
/// The first Click is raised as soon as the pointer presses the button. After <see cref="Delay"/> milliseconds the button
/// repeats it every <see cref="Interval"/> milliseconds until the pointer is released. Releasing does not raise another Click.
/// While the pointer is held outside the button the repeating pauses, and it goes on when the pointer comes back.
/// </para>
/// <para>
/// A RepeatButton is styled as a <c>Button</c>, so it takes the Button rules of the theme. While it is held it is
/// <see cref="Control.IsAnimating"/>, so the window draws frames and the button reads the time from
/// <see cref="Element.AnimationTime"/>. Without an <see cref="Element.AnimationClock"/> it only raises the first Click.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var up = new RepeatButton { Text = "+", Delay = 500, Interval = 50 };
///
/// up.Click += (_, _) => number.Value++;
/// </code>
/// </example>
public class RepeatButton : Button
{
    /// <summary>The default time before the first repeat, in milliseconds.</summary>
    public const double DefaultDelay = 400;

    /// <summary>The default time between repeats, in milliseconds.</summary>
    public const double DefaultInterval = 60;

    TimeSpan? nextRepeat;

    /// <summary>Gets or sets the time between the press and the first repeat, in milliseconds. The default is 400.</summary>
    public double Delay { get; set; } = DefaultDelay;

    /// <summary>Gets or sets the time between repeats, in milliseconds. The default is 60.</summary>
    public double Interval { get; set; } = DefaultInterval;

    /// <summary>Gets whether the button is held down and so repeating.</summary>
    public bool IsRepeating => nextRepeat is not null;

    /// <inheritdoc/>
    public override string TypeName => "Button";

    /// <inheritdoc/>
    public override bool IsAnimating => IsRepeating;

    /// <summary>Presses the button, raises the first Click and starts the repeating.</summary>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public override bool PointerDown()
    {
        var changed = base.PointerDown();

        nextRepeat = AnimationTime + TimeSpan.FromMilliseconds(Delay);

        return changed | RaiseClick();
    }

    /// <summary>Releases the button and stops the repeating. The release is not a click.</summary>
    /// <param name="inside">Ignored.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public override bool PointerUp(bool inside)
    {
        nextRepeat = null;

        return SetPressed(false);
    }

    /// <inheritdoc/>
    protected internal override bool OnFrame()
    {
        if (nextRepeat is not { } due || !PseudoStates.Contains("hover"))
            return false;

        var now = AnimationTime;
        var interval = TimeSpan.FromMilliseconds(Math.Max(1, Interval));
        var changed = false;

        while (now >= due)
        {
            changed |= RaiseClick();

            due += interval;
        }

        nextRepeat = due;

        return changed;
    }
}
