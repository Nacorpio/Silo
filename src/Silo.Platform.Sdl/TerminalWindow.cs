using Silo.Styling;

namespace Silo.Platform.Sdl;

/// <summary>
/// An operating-system window that shows a <see cref="Terminal"/> connected to a session, for a console that opens next to the
/// app instead of inside it.
/// </summary>
/// <remarks>
/// <para>
/// The window opens at the size that fits the columns and rows, in the font of the Stylesheets, and the terminal fills the
/// window while the user resizes it. The title of the window follows the title that the program sets. Output that a session
/// writes from another thread is handed to the window's thread. When the user closes the window the session is disposed, and
/// when the session ends the window closes, unless <see cref="CloseOnExit"/> is <see langword="false"/>.
/// </para>
/// <para>
/// A terminal window is an ordinary <see cref="SdlWindow"/>, so it can be opened next to the main window of an app and shares
/// its event loop. Run the app with <see cref="SdlWindow.RunAll"/> to keep going until the last window is closed.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var session = new CommandSession { Banner = "Silo console\n" };
///
/// session.Add("greet", "Says hello", context => context.WriteLine("Hello!"));
///
/// using var console = new TerminalWindow(session, "Console", styles: new StyleResolver([DefaultTheme.Load()]));
///
/// console.Run();
/// </code>
/// </example>
public sealed class TerminalWindow : IDisposable
{
    /// <summary>The window that the terminal is in.</summary>
    readonly Window content;

    /// <summary>The platform window.</summary>
    readonly SdlWindow window;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    bool disposed;

    /// <summary>Opens a window with a terminal and starts the session in it.</summary>
    /// <param name="session">The session. The window disposes it when it is closed.</param>
    /// <param name="title">The title until the program sets one.</param>
    /// <param name="columns">The number of columns the window opens with.</param>
    /// <param name="rows">The number of rows the window opens with.</param>
    /// <param name="styles">The Stylesheets, which give the terminal its font, colors and padding. Without them the terminal is unstyled.</param>
    /// <exception cref="InvalidOperationException">SDL could not start or could not create the window.</exception>
    public TerminalWindow(ITerminalSession session, string title = "Terminal", int columns = 80, int rows = 24, StyleResolver? styles = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        Terminal = new Terminal(columns, rows) { Clipboard = new SdlClipboard() };

        Terminal.AddClass("fill");

        content = new Window { Title = title, TextMeasurer = new Silo.Rendering.Skia.SkiaTextMeasurer() };

        content.Children.Add(Terminal);

        if (styles is not null)
        {
            content.ApplyStyles(styles);

            var size = Terminal.SizeFor(columns, rows);

            if (size.Width > 0)
                (content.InitialWidth, content.InitialHeight) = ((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        }

        window = new SdlWindow(content) { Styles = styles };

        window.Focus(Terminal);

        Terminal.TitleChanged += (_, _) => content.Title = Terminal.Buffer.Title.Length > 0 ? Terminal.Buffer.Title : title;
        Terminal.Exited += (_, _) =>
        {
            if (CloseOnExit)
                content.Close();
        };

        window.Closed += (_, _) => Dispose();

        Terminal.Attach(session, work => window.Post(() =>
        {
            work();
            window.Invalidate();
        }));
    }

    /// <summary>Gets the terminal in the window.</summary>
    public Terminal Terminal { get; }

    /// <summary>Gets the session that the terminal shows.</summary>
    public ITerminalSession Session { get; }

    /// <summary>Gets the platform window, for example to attach the Inspector to it or to show a toast.</summary>
    public SdlWindow Window => window;

    /// <summary>Gets or sets whether the window closes when the session ends. The default is <see langword="true"/>.</summary>
    public bool CloseOnExit { get; set; } = true;

    /// <summary>Handles events until this window is closed.</summary>
    public void Run() => window.Run();

    /// <summary>Disposes the session and closes the window.</summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        Terminal.Detach();
        Session.Dispose();
        window.Dispose();
    }
}
