namespace Silo;

/// <summary>
/// The root Control of an application window: it holds the title and the initial size, and fills the client area.
/// </summary>
/// <remarks>
/// <para>
/// A Window is a container like <see cref="Box"/>, so its children are the content of the window. Hand it to the platform
/// window, which opens a native window with the <see cref="Title"/> and the initial size and keeps the title up to date when
/// it changes. Without a <c>width</c> and <c>height</c> it fills the client area, also while the user resizes the window.
/// </para>
/// <para>
/// The Default theme gives a Window the surface color and the default font, and stacks its children in a column.
/// <see cref="Control.Overlays"/> of a Window shown by a platform window is the layer for menus and tooltips.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var window = new Window { Title = "Notes", InitialWidth = 640, InitialHeight = 400 };
///
/// window.Children.Add(new TextBlock { Text = "Hello" });
///
/// using var native = new SdlWindow(window);
/// </code>
/// </example>
public class Window : Control
{
    /// <summary>The window title.</summary>
    string title = string.Empty;

    /// <summary>What carries out the state changes.</summary>
    IWindowHost? host;

    /// <summary>Whether the window is to be shown above the others.</summary>
    bool alwaysOnTop;


    /// <summary>Raised after <see cref="Title"/> changed.</summary>
    public event EventHandler? TitleChanged;


    /// <summary>Gets or sets the title that the platform window shows in its title bar. The default is an empty string.</summary>
    public string Title
    {
        get => title;
        set
        {
            var next = value ?? string.Empty;

            if (next == title)
                return;

            title = next;

            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the width of the client area in logical units when the platform window is opened. The default is 800.</summary>
    /// <remarks>The user can resize the window afterwards, and the Window fills it. Changing this later does nothing.</remarks>
    public int InitialWidth { get; set; } = 800;

    /// <summary>Gets or sets the height of the client area in logical units when the platform window is opened. The default is 600.</summary>
    /// <remarks>The user can resize the window afterwards, and the Window fills it. Changing this later does nothing.</remarks>
    public int InitialHeight { get; set; } = 600;

    /// <summary>Raised when the window was maximized or restored.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Gets or sets what carries out the state changes, which the platform window sets when it opens.</summary>
    /// <remarks>Setting it hands <see cref="AlwaysOnTop"/> to the new host.</remarks>
    public IWindowHost? Host
    {
        get => host;
        set
        {
            host = value;

            if (host is not null && alwaysOnTop)
                host.AlwaysOnTop = true;
        }
    }

    /// <summary>
    /// Gets or sets whether the window is shown above all other windows, also while another application has the focus, for
    /// example a small tool or a console that should stay in view. It can be set before the platform window opens and changed
    /// while it is open. The default is <see langword="false"/>.
    /// </summary>
    public bool AlwaysOnTop
    {
        get => host?.AlwaysOnTop ?? alwaysOnTop;
        set
        {
            alwaysOnTop = value;

            if (host is not null)
                host.AlwaysOnTop = value;
        }
    }

    /// <summary>Gets whether the window is maximized.</summary>
    public bool IsMaximized => Host?.IsMaximized ?? false;

    /// <summary>Minimizes the window.</summary>
    public void Minimize() => Host?.Minimize();

    /// <summary>Maximizes the window.</summary>
    public void Maximize() => Host?.Maximize();

    /// <summary>Restores the window from the maximized or minimized state.</summary>
    public void Restore() => Host?.Restore();

    /// <summary>Closes the window.</summary>
    public void Close() => Host?.Close();

    /// <summary>Raises <see cref="StateChanged"/>. The platform window calls it.</summary>
    public void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
