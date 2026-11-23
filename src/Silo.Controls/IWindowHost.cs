namespace Silo;

/// <summary>What a native window offers a <see cref="Window"/> Control, so that the Control can ask for state changes without knowing the platform.</summary>
public interface IWindowHost
{
    /// <summary>Gets whether the window is maximized.</summary>
    bool IsMaximized { get; }

    /// <summary>Minimizes the window.</summary>
    void Minimize();

    /// <summary>Maximizes the window.</summary>
    void Maximize();

    /// <summary>Restores the window from the maximized or minimized state.</summary>
    void Restore();

    /// <summary>Closes the window.</summary>
    void Close();
}
