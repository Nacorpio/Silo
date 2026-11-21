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

    /// <summary>The root of the Control tree.</summary>
    readonly Control root;

    /// <summary>Routes the pointer into the tree.</summary>
    readonly PointerRouter router;

    /// <summary>Work that other threads asked the window thread to do.</summary>
    readonly ConcurrentQueue<Action> posted = new();

    /// <summary>The SDL window.</summary>
    readonly SDL_Window* window;

    /// <summary>The Stylesheets the Controls are styled with.</summary>
    StyleResolver? styles;

    /// <summary>Whether the next <see cref="Pump"/> needs to draw a frame.</summary>
    bool dirty = true;

    /// <summary>Whether the window was closed.</summary>
    bool closed;

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

        window = SDL3.SDL_CreateWindow(title, width, height, SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);

        if (window is null)
            throw new InvalidOperationException($"SDL could not create the window: {SDL3.SDL_GetError()}");

        this.root = root;

        root.TextMeasurer ??= new SkiaTextMeasurer();
        router = new PointerRouter(root);
    }

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
    /// <returns><see langword="false"/> once the window has been closed.</returns>
    public bool Pump(int timeoutMilliseconds)
    {
        SDL_Event e;

        if (SDL3.SDL_WaitEventTimeout(&e, timeoutMilliseconds))
            do
                Handle(e);
            while (SDL3.SDL_PollEvent(&e));

        while (posted.TryDequeue(out var work))
            work();

        if (dirty && !closed)
            Draw();

        return !closed;
    }

    /// <summary>Closes the window.</summary>
    public void Dispose()
    {
        SDL3.SDL_DestroyWindow(window);
        SDL3.SDL_Quit();
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
            SDL_EventType.SDL_EVENT_QUIT or SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED => Close(),

            SDL_EventType.SDL_EVENT_MOUSE_MOTION => router.Move(e.motion.x, e.motion.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == 1 => router.Down(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when e.button.button == 1 => router.Up(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE => router.Leave(),

            SDL_EventType.SDL_EVENT_WINDOW_EXPOSED or SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED => true,

            _ => false,
        };

        if (changed)
            Restyle();
    }

    /// <summary>Marks the window as closed.</summary>
    /// <returns>Always <see langword="false"/>, so that no frame is drawn.</returns>
    bool Close()
    {
        closed = true;

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

        using var bitmap = new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));

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
