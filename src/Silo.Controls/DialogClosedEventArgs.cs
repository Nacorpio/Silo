namespace Silo;

/// <summary>Describes how a <see cref="Dialog"/> was closed.</summary>
/// <param name="result">The result of the button that was pressed, or <see langword="null"/> when it was dismissed.</param>
public sealed class DialogClosedEventArgs(object? result) : EventArgs
{
    /// <summary>Gets the result of the button that was pressed, or <see langword="null"/> when the Dialog was dismissed.</summary>
    public object? Result { get; } = result;

    /// <summary>Gets whether the Dialog was dismissed, for example with Escape, instead of closed by a button.</summary>
    public bool IsDismissed => Result is null;
}
