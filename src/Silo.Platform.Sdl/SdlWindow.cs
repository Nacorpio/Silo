using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    /// <summary>The Controls drawn above the root.</summary>
    readonly OverlayLayer overlays;

    /// <summary>Shows the tooltip of the Control that the pointer rests on.</summary>
    readonly TooltipPresenter tooltips;

    /// <summary>Tells how long the window has been running, for the tooltips' delay.</summary>
    readonly Stopwatch clock = Stopwatch.StartNew();

    /// <summary>Keeps the keyboard focus.</summary>
    readonly FocusManager focus = new();

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

    /// <summary>Whether the window draws its own title bar.</summary>
    bool customTitleBar;

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
        overlays = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        root.Overlays = overlays;
        tooltips = new TooltipPresenter(overlays);
        Toasts = new ToastHost(overlays);
        overlays.Changed += (_, _) => Restyle();
        router = new PointerRouter(root, overlays);

        SDL3.SDL_StartTextInput(window);

        Open.Add(this);
    }

    /// <summary>Creates a window for a <see cref="Window"/> Control and shows it.</summary>
    /// <param name="content">
    /// The root Control. Its <see cref="Window.Title"/> and initial size open the window, and the title of the native
    /// window follows it from then on.
    /// </param>
    /// <exception cref="InvalidOperationException">SDL could not start or could not create the window.</exception>
    public SdlWindow(Window content)
        : this(Checked(content).Title, content.InitialWidth, content.InitialHeight, content)
    {
        content.TitleChanged += (_, _) => SetTitle(content.Title);
        content.Host = new Host(this);
    }

    /// <summary>Raised when a key is pressed while this window has the keyboard focus.</summary>
    public event Action<Key>? KeyPressed;

    /// <summary>Raised when the user closes the window, after which the window no longer draws or handles input.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets the root Control of the window.</summary>
    public Control Root => root;

    /// <summary>Gets the layer for menus, drop-down lists and tooltips, which are drawn above the root Control.</summary>
    public OverlayLayer Overlays => overlays;

    /// <summary>Gets the host that shows toasts in a corner of the window.</summary>
    /// <remarks>Use it from the UI thread, or from <see cref="Post"/>.</remarks>
    public ToastHost Toasts { get; }

    /// <summary>Gets the id that SDL puts in the events of this window.</summary>
    public uint Id => id;

    /// <summary>
    /// Gets or sets whether the window has no native frame and draws its own title bar, for example with a
    /// <see cref="TitleBar"/>. The operating system still moves and resizes the window: SDL asks which points are drag areas
    /// and which are resize edges, and <see cref="WindowHitTester"/> answers. The default is <see langword="false"/>.
    /// </summary>
    public bool CustomTitleBar
    {
        get => customTitleBar;
        set
        {
            if (value == customTitleBar)
                return;

            customTitleBar = value;

            SDL3.SDL_SetWindowBordered(window, !value);
            InstallHitTest(value);

            dirty = true;
        }
    }

    /// <summary>Gets whether the window is maximized.</summary>
    public bool IsMaximized => !disposed && !closed && (SDL3.SDL_GetWindowFlags(window) & SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0;

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

        var wait = Open.Select(open => open.DueMilliseconds()).Prepend(timeoutMilliseconds).Min();

        if (SDL3.SDL_WaitEventTimeout(&e, wait))
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
        SDL_EventType.SDL_EVENT_TEXT_INPUT => (uint)e.text.windowID,
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
        SDL_Keycode.SDLK_BACKSPACE => Key.Backspace,
        SDL_Keycode.SDLK_DELETE => Key.Delete,
        SDL_Keycode.SDLK_LEFT => Key.Left,
        SDL_Keycode.SDLK_RIGHT => Key.Right,
        SDL_Keycode.SDLK_HOME => Key.Home,
        SDL_Keycode.SDLK_END => Key.End,
        SDL_Keycode.SDLK_RETURN => Key.Enter,
        SDL_Keycode.SDLK_UP => Key.Up,
        SDL_Keycode.SDLK_DOWN => Key.Down,
        SDL_Keycode.SDLK_TAB => Key.Tab,
        SDL_Keycode.SDLK_PAGEUP => Key.PageUp,
        SDL_Keycode.SDLK_PAGEDOWN => Key.PageDown,
        >= SDL_Keycode.SDLK_A and <= SDL_Keycode.SDLK_Z => Key.A + (int)(code - SDL_Keycode.SDLK_A),

        _ => Key.Unknown,
    };

    /// <summary>Checks that a Window was given.</summary>
    /// <param name="content">The Window.</param>
    /// <returns>The same Window.</returns>
    static Window Checked(Window content) => content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>Changes the title of the native window.</summary>
    /// <param name="title">The new title.</param>
    void SetTitle(string title)
    {
        if (!disposed && !closed)
            SDL3.SDL_SetWindowTitle(window, title);
    }

    /// <summary>Runs the posted work and draws a frame if one is needed.</summary>
    void Tick()
    {
        while (posted.TryDequeue(out var work))
            work();

        if (tooltips.Tick(clock.Elapsed) | Toasts.Tick(clock.Elapsed))
            dirty = true;

        if (dirty && !closed)
            Draw();
    }

    /// <summary>Tells how long until a tooltip is due or a toast runs out, so that the event loop wakes up for it.</summary>
    /// <returns>The time in milliseconds, or <see cref="int.MaxValue"/> when nothing is waiting.</returns>
    int DueMilliseconds()
    {
        var tooltip = tooltips.DueIn(clock.Elapsed);
        var toast = Toasts.DueIn();
        var due = tooltip is null ? toast : toast is null ? tooltip : tooltip < toast ? tooltip : toast;

        return due is { } time ? (int)Math.Ceiling(time.TotalMilliseconds) : int.MaxValue;
    }

    /// <summary>Applies the Stylesheets to the tree and asks for a frame.</summary>
    void Restyle()
    {
        if (styles is not null)
        {
            root.ApplyStyles(styles);
            overlays.ApplyStyles(styles);
        }

        dirty = true;
    }

    /// <summary>Reacts to one SDL event.</summary>
    /// <param name="e">The event.</param>
    void Handle(SDL_Event e)
    {
        var changed = (SDL_EventType)e.type switch
        {
            SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED => Close(),

            SDL_EventType.SDL_EVENT_MOUSE_MOTION => MovePointer(e.motion.x, e.motion.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == 1 => PressPointer(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == 3 => router.SecondaryDown(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when e.button.button == 1 => router.Up(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE => router.Leave() | tooltips.Dismiss(),
            SDL_EventType.SDL_EVENT_MOUSE_WHEEL
                => tooltips.Dismiss() | router.Scroll(e.wheel.mouse_x, e.wheel.mouse_y, -e.wheel.y * WheelStep),

            SDL_EventType.SDL_EVENT_KEY_DOWN => PressKey(e.key),
            SDL_EventType.SDL_EVENT_TEXT_INPUT => focus.TextInput(Marshal.PtrToStringUTF8((IntPtr)e.text.text) ?? ""),

            SDL_EventType.SDL_EVENT_WINDOW_EXPOSED or SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED => true,
            SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED or SDL_EventType.SDL_EVENT_WINDOW_RESTORED or SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED
                => StateChanged(),

            _ => false,
        };

        if (changed)
            Restyle();
    }

    /// <summary>Moves the pointer and tells the tooltips where it rests.</summary>
    /// <param name="x">The horizontal position in logical units.</param>
    /// <param name="y">The vertical position in logical units.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed or a tooltip was hidden.</returns>
    bool MovePointer(float x, float y)
    {
        var changed = router.Move(x, y);

        return tooltips.Hover(router.Hovered, x, y, clock.Elapsed) | changed;
    }

    /// <summary>Converts SDL modifier flags to <see cref="KeyModifiers"/>.</summary>
    /// <param name="mod">The SDL modifier flags.</param>
    /// <returns>The modifiers. The Command key of a Mac counts as Control.</returns>
    static KeyModifiers ToModifiers(SDL_Keymod mod)
    {
        var result = KeyModifiers.None;

        if ((mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0)
            result |= KeyModifiers.Shift;

        if ((mod & (SDL_Keymod.SDL_KMOD_CTRL | SDL_Keymod.SDL_KMOD_GUI)) != 0)
            result |= KeyModifiers.Control;

        if ((mod & SDL_Keymod.SDL_KMOD_ALT) != 0)
            result |= KeyModifiers.Alt;

        return result;
    }

    /// <summary>Raises <see cref="KeyPressed"/> for a key press that is not an auto-repeat.</summary>
    /// <param name="key">The SDL keyboard event.</param>
    /// <returns>Always <see langword="false"/>, since a key press changes nothing by itself.</returns>
    bool PressKey(SDL_KeyboardEvent key)
    {
        var pressed = ToKey(key.key);
        var hidden = tooltips.Dismiss();

        if (!key.repeat)
            KeyPressed?.Invoke(pressed);

        var modifiers = ToModifiers(key.mod);

        if (pressed == Key.Escape && overlays.CloseTop())
            return true;

        if (MenuBar.AccessKeyDown(root, pressed, modifiers))
            return true;

        if (overlays.ModalCount > 0)
            return overlays.KeyDown(pressed, modifiers) | hidden;

        return focus.KeyDown(pressed, modifiers) | hidden;
    }

    /// <summary>Presses the pointer button and moves the keyboard focus to what was pressed.</summary>
    /// <param name="x">The horizontal position in logical units.</param>
    /// <param name="y">The vertical position in logical units.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    bool PressPointer(float x, float y)
    {
        var overlaid = overlays.ModalCount > 0;
        router.Modifiers = ToModifiers(SDL3.SDL_GetModState());

        var changed = router.Down(x, y) | tooltips.Dismiss();

        return (!overlaid && focus.FocusFrom(router.Hovered)) | changed;
    }

    /// <summary>Turns the callback on or off that tells SDL what the points of the window are.</summary>
    /// <param name="enabled">Whether SDL should ask.</param>
    void InstallHitTest(bool enabled)
    {
        if (enabled)
        {
            SDL3.SDL_SetWindowHitTest(window, &HitTest, IntPtr.Zero);

            return;
        }

        SDL3.SDL_SetWindowHitTest(window, null, IntPtr.Zero);
    }

    /// <summary>Tells the Window Control that the native window was maximized, minimized or restored.</summary>
    /// <returns>Always <see langword="true"/>, so that a frame is drawn with the new button glyph.</returns>
    bool StateChanged()
    {
        (root as Window)?.NotifyStateChanged();

        return true;
    }

    /// <summary>Answers SDL when it asks what a point of a borderless window is.</summary>
    /// <param name="win">The SDL window.</param>
    /// <param name="area">The point.</param>
    /// <param name="data">Not used.</param>
    /// <returns>Whether the point drags the window, resizes it, or is an ordinary point.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    static SDL_HitTestResult HitTest(SDL_Window* win, SDL_Point* area, nint data)
    {
        var target = Open.FirstOrDefault(open => open.window == win);

        if (target is null)
            return SDL_HitTestResult.SDL_HITTEST_NORMAL;

        int width, height;

        SDL3.SDL_GetWindowSize(win, &width, &height);

        var region = WindowHitTester.Test(target.root, area->x, area->y, width, height, !target.IsMaximized);

        return region switch
        {
            WindowRegion.Drag => SDL_HitTestResult.SDL_HITTEST_DRAGGABLE,
            WindowRegion.ResizeTop => SDL_HitTestResult.SDL_HITTEST_RESIZE_TOP,
            WindowRegion.ResizeBottom => SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOM,
            WindowRegion.ResizeLeft => SDL_HitTestResult.SDL_HITTEST_RESIZE_LEFT,
            WindowRegion.ResizeRight => SDL_HitTestResult.SDL_HITTEST_RESIZE_RIGHT,
            WindowRegion.ResizeTopLeft => SDL_HitTestResult.SDL_HITTEST_RESIZE_TOPLEFT,
            WindowRegion.ResizeTopRight => SDL_HitTestResult.SDL_HITTEST_RESIZE_TOPRIGHT,
            WindowRegion.ResizeBottomLeft => SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOMLEFT,
            WindowRegion.ResizeBottomRight => SDL_HitTestResult.SDL_HITTEST_RESIZE_BOTTOMRIGHT,

            _ => SDL_HitTestResult.SDL_HITTEST_NORMAL,
        };
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

        overlays.Arrange(new Size(width, height));
        overlays.Render(builder);

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

    /// <summary>Carries out the state changes that a <see cref="Window"/> Control asks for.</summary>
    /// <param name="owner">The native window.</param>
    sealed class Host(SdlWindow owner) : IWindowHost
    {
        /// <inheritdoc/>
        public bool IsMaximized => owner.IsMaximized;

        /// <inheritdoc/>
        public void Minimize() => SDL3.SDL_MinimizeWindow(owner.window);

        /// <inheritdoc/>
        public void Maximize() => SDL3.SDL_MaximizeWindow(owner.window);

        /// <inheritdoc/>
        public void Restore() => SDL3.SDL_RestoreWindow(owner.window);

        /// <inheritdoc/>
        public void Close() => owner.Close();
    }
}
