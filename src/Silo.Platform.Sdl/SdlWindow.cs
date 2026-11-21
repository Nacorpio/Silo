using System.Collections.Concurrent;
using SDL;
using SkiaSharp;
using Silo.Layout;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo.Platform.Sdl;

/// <summary>
/// An operating-system window, created with SDL3, that shows a tree of Controls and sends it the pointer.
/// </summary>
/// <remarks>
/// <para>
/// The window lays the root out at the window's size, builds a display list, replays it with Skia on a bitmap, and copies the
/// bitmap to the window. A high-density display is handled by drawing at the pixel size with the canvas scaled, so Controls
/// are laid out in logical units.
/// </para>
/// <para>
/// Everything runs on the thread that created the window. To change anything from another thread, such as a hot-reloaded
/// Stylesheet, use <see cref="Post"/>.
/// </para>
/// <para>
/// A process can have several windows, such as an app and its Inspector. They share one event loop: pumping any window
/// handles the events of all of them, and an event goes to the window that it was meant for.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var root = new Box { Name = "root" };
///
/// root.Children.Add(new Button { Text = "Hello" });
///
/// using var window = new SdlWindow("Silo", 640, 480, root);
///
/// window.Styles = new StyleResolver([DefaultTheme.Load()]);
/// window.Run();
/// </code>
/// </example>
public sealed unsafe class SdlWindow : IDisposable
{
    /// <summary>The longest <see cref="Run"/> waits for an event before it checks again, in milliseconds.</summary>
    const int IdleMilliseconds = 100;

    /// <summary>How many pixels one notch of the mouse wheel scrolls.</summary>
    const double WheelStep = 40;


    /// <summary>The windows that are open, which share the event loop.</summary>
    static readonly List<SdlWindow> Open = [];


    /// <summary>The root of the Control tree.</summary>
    readonly Control root;

    /// <summary>Routes the pointer into the tree.</summary>
    readonly PointerRouter router;

    /// <summary>Work that other threads asked the window thread to do.</summary>
    readonly ConcurrentQueue<Action> posted = new();

    /// <summary>The SDL window.</summary>
    readonly SDL_Window* window;

    /// <summary>The id that SDL puts in the events of this window.</summary>
    readonly uint id;


    /// <summary>The Stylesheets the Controls are styled with.</summary>
    StyleResolver? styles;

    /// <summary>Whether the next <see cref="Pump"/> needs to draw a frame.</summary>
    bool dirty = true;

    /// <summary>Whether the window was closed.</summary>
    bool closed;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    bool disposed;

    /// <summary>Creates a window and shows it.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="width">The initial width in logical units.</param>
    /// <param name="height">The initial height in logical units.</param>
    /// <param name="root">The root Control. Its <see cref="Control.TextMeasurer"/> is set to a Skia one when it has none.</param>
    /// <exception cref="InvalidOperationException">SDL could not start or could not create the window.</exception>
    public SdlWindow(string title, int width, int height, Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL could not start: {SDL3.SDL_GetError()}");

        var flags = SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY;

        window = SDL3.SDL_CreateWindow(title, width, height, flags);

        if (window is null)
            throw new InvalidOperationException($"SDL could not create the window: {SDL3.SDL_GetError()}");

        this.root = root;

        id = (uint)SDL3.SDL_GetWindowID(window);
        root.TextMeasurer ??= new SkiaTextMeasurer();
        router = new PointerRouter(root);

        Open.Add(this);
    }

    /// <summary>Raised when a key is pressed while this window has the keyboard focus.</summary>
    public event Action<Key>? KeyPressed;

    /// <summary>Raised when the user closes the window, after which the window no longer draws or handles input.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets the root Control of the window.</summary>
    public Control Root => root;

    /// <summary>Gets the id that SDL puts in the events of this window.</summary>
    public uint Id => id;

    /// <summary>Gets or sets the color that every frame is cleared with. The default is white.</summary>
    public Color Background { get; set; } = new(255, 255, 255);

    /// <summary>Gets or sets the Stylesheets to style the tree with. Setting it styles the tree and draws a new frame.</summary>
    public StyleResolver? Styles
    {
        get => styles;
        set
        {
            styles = value;

            Restyle();
        }
    }

    /// <summary>Gets how many frames have been drawn.</summary>
    public int Frames { get; private set; }

    /// <summary>Runs work on the window's thread. It is safe to call from any thread.</summary>
    /// <param name="work">The work to run.</param>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        posted.Enqueue(work);

        SDL_Event wake = default;

        wake.type = (uint)SDL_EventType.SDL_EVENT_USER;

        SDL3.SDL_PushEvent(&wake);
    }

    /// <summary>Asks for a new frame.</summary>
    public void Invalidate() => dirty = true;

    /// <summary>Handles events until the window is closed.</summary>
    public void Run()
    {
        while (Pump(IdleMilliseconds))
        {
        }
    }

    /// <summary>Waits for an event, handles everything that is queued, and draws a frame if something changed.</summary>
    /// <param name="timeoutMilliseconds">The longest to wait for an event.</param>
    /// <returns><see langword="false"/> once this window has been closed.</returns>
    /// <remarks>Events are handled for every open window, not only this one, and every window that changed draws a frame.</remarks>
    public bool Pump(int timeoutMilliseconds)
    {
        SDL_Event e;

        if (SDL3.SDL_WaitEventTimeout(&e, timeoutMilliseconds))
            do
                Dispatch(e);
            while (SDL3.SDL_PollEvent(&e));

        foreach (var open in Open.ToArray())
            open.Tick();

        return !closed;
    }

    /// <summary>Closes the window.</summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        Open.Remove(this);

        SDL3.SDL_DestroyWindow(window);
        SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
    }

    /// <summary>Sends an event to the window that it was meant for.</summary>
    /// <param name="e">The event.</param>
    static void Dispatch(SDL_Event e)
    {
        if ((SDL_EventType)e.type == SDL_EventType.SDL_EVENT_QUIT)
        {
            foreach (var open in Open.ToArray())
                open.Close();

            return;
        }

        var target = WindowIdOf(e);

        (Open.FirstOrDefault(w => w.id == target) ?? Open.FirstOrDefault())?.Handle(e);
    }

    /// <summary>Reads the id of the window that an event is for.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The window id, or 0 for an event that does not name a window.</returns>
    static uint WindowIdOf(SDL_Event e) => (SDL_EventType)e.type switch
    {
        SDL_EventType.SDL_EVENT_MOUSE_MOTION => (uint)e.motion.windowID,
        SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN or SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP => (uint)e.button.windowID,
        SDL_EventType.SDL_EVENT_KEY_DOWN => (uint)e.key.windowID,
        SDL_EventType.SDL_EVENT_MOUSE_WHEEL => (uint)e.wheel.windowID,
        SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED
            or SDL_EventType.SDL_EVENT_WINDOW_EXPOSED
            or SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED
            or SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE => (uint)e.window.windowID,

        _ => 0,
    };

    /// <summary>Converts an SDL key code to a <see cref="Key"/>.</summary>
    /// <param name="code">The SDL key code.</param>
    /// <returns>The key, or <see cref="Key.Unknown"/>.</returns>
    static Key ToKey(SDL_Keycode code) => code switch
    {
        SDL_Keycode.SDLK_ESCAPE => Key.Escape,
        SDL_Keycode.SDLK_F5 => Key.F5,
        SDL_Keycode.SDLK_F12 => Key.F12,

        _ => Key.Unknown,
    };

    /// <summary>Runs the posted work and draws a frame if one is needed.</summary>
    void Tick()
    {
        while (posted.TryDequeue(out var work))
            work();

        if (dirty && !closed)
            Draw();
    }

    /// <summary>Applies the Stylesheets to the tree and asks for a frame.</summary>
    void Restyle()
    {
        if (styles is not null)
            root.ApplyStyles(styles);

        dirty = true;
    }

    /// <summary>Reacts to one SDL event.</summary>
    /// <param name="e">The event.</param>
    void Handle(SDL_Event e)
    {
        var changed = (SDL_EventType)e.type switch
        {
            SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED => Close(),

            SDL_EventType.SDL_EVENT_MOUSE_MOTION => router.Move(e.motion.x, e.motion.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == 1 => router.Down(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when e.button.button == 1 => router.Up(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE => router.Leave(),
            SDL_EventType.SDL_EVENT_MOUSE_WHEEL
                => router.Scroll(e.wheel.mouse_x, e.wheel.mouse_y, -e.wheel.y * WheelStep),

            SDL_EventType.SDL_EVENT_KEY_DOWN => PressKey(e.key),

            SDL_EventType.SDL_EVENT_WINDOW_EXPOSED or SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED => true,

            _ => false,
        };

        if (changed)
            Restyle();
    }

    /// <summary>Raises <see cref="KeyPressed"/> for a key press that is not an auto-repeat.</summary>
    /// <param name="key">The SDL keyboard event.</param>
    /// <returns>Always <see langword="false"/>, since a key press changes nothing by itself.</returns>
    bool PressKey(SDL_KeyboardEvent key)
    {
        if (!key.repeat)
            KeyPressed?.Invoke(ToKey(key.key));

        return false;
    }

    /// <summary>Marks the window as closed, takes it out of the event loop and raises <see cref="Closed"/>.</summary>
    /// <returns>Always <see langword="false"/>, so that no frame is drawn.</returns>
    bool Close()
    {
        if (closed)
            return false;

        closed = true;

        Open.Remove(this);
        Closed?.Invoke(this, EventArgs.Empty);

        return false;
    }

    /// <summary>Lays the tree out, draws it and copies it to the window.</summary>
    void Draw()
    {
        dirty = false;

        int width, height, pixelWidth, pixelHeight;

        SDL3.SDL_GetWindowSize(window, &width, &height);
        SDL3.SDL_GetWindowSizeInPixels(window, &pixelWidth, &pixelHeight);

        if (width <= 0 || height <= 0)
            return;

        root.Arrange(new Size(width, height));

        var builder = new DisplayListBuilder();

        root.Render(builder);

        using var bitmap =
            new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(Background.R, Background.G, Background.B, Background.A));
            canvas.Scale((float)pixelWidth / width, (float)pixelHeight / height);
            builder.Build().Replay(new SkiaDrawingContext(canvas));
        }

        Present(bitmap);

        Frames++;
    }

    /// <summary>Copies a bitmap to the window surface and shows it.</summary>
    /// <param name="bitmap">The frame, in BGRA order.</param>
    void Present(SKBitmap bitmap)
    {
        var surface = SDL3.SDL_GetWindowSurface(window);

        if (surface is null || !SDL3.SDL_LockSurface(surface))
            return;

        var rows = Math.Min(bitmap.Height, surface->h);
        var bytes = Math.Min(bitmap.RowBytes, surface->pitch);
        var source = (byte*)bitmap.GetPixels();
        var target = (byte*)surface->pixels;

        for (var y = 0; y < rows; y++)
            Buffer.MemoryCopy(source + (long)y * bitmap.RowBytes, target + (long)y * surface->pitch, bytes, bytes);

        SDL3.SDL_UnlockSurface(surface);
        SDL3.SDL_UpdateWindowSurface(window);
    }
}
