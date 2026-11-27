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


    /// <summary>Runs the content of the window: the Control tree, its overlays, the input rules and the steps of a frame.</summary>
    readonly WindowScene scene;

    /// <summary>Tells how long the window has been running, for the scene's clock.</summary>
    readonly Stopwatch clock = Stopwatch.StartNew();

    /// <summary>Work that other threads asked the window thread to do.</summary>
    readonly ConcurrentQueue<Action> posted = new();

    /// <summary>The SDL window.</summary>
    readonly SDL_Window* window;

    /// <summary>The id that SDL puts in the events of this window.</summary>
    readonly uint id;


    /// <summary>Whether the window was closed.</summary>
    bool closed;

    /// <summary>Whether the window was asked to be shown above the others.</summary>
    bool alwaysOnTop;

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

        id = (uint)SDL3.SDL_GetWindowID(window);
        root.TextMeasurer ??= new SkiaTextMeasurer();
        scene = new WindowScene(root, new StopwatchClock(clock), FrameTime(window));

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
    public Control Root => scene.Root;

    /// <summary>Gets the layer for menus, drop-down lists and tooltips, which are drawn above the root Control.</summary>
    public OverlayLayer Overlays => scene.Overlays;

    /// <summary>Gets the host that shows toasts in a corner of the window.</summary>
    /// <remarks>Use it from the UI thread, or from <see cref="Post"/>.</remarks>
    public ToastHost Toasts => scene.Toasts;

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

            scene.Invalidate();
        }
    }

    /// <summary>Gets whether the window is maximized.</summary>
    public bool IsMaximized => !disposed && !closed && (SDL3.SDL_GetWindowFlags(window) & SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0;

    /// <summary>
    /// Gets or sets whether the window is shown above all other windows, also while another application has the focus. The default
    /// is <see langword="false"/>. How a window manager treats it can differ, and some ignore it.
    /// </summary>
    public bool AlwaysOnTop
    {
        get => alwaysOnTop;
        set
        {
            alwaysOnTop = value;

            if (!disposed && !closed)
                SDL3.SDL_SetWindowAlwaysOnTop(window, value);
        }
    }

    /// <summary>
    /// Gets or sets whether the user prefers little motion, so that transitions other than those of <c>opacity</c> are skipped. Set it from
    /// the operating system's accessibility setting. The default is <see langword="false"/>.
    /// </summary>
    public bool ReducedMotion
    {
        get => scene.ReducedMotion;
        set => scene.ReducedMotion = value;
    }

    /// <summary>Gets or sets the color that every frame is cleared with. The default is white.</summary>
    public Color Background { get; set; } = new(255, 255, 255);

    /// <summary>Gets or sets the Stylesheets to style the tree with. Setting it styles the tree and draws a new frame.</summary>
    public StyleResolver? Styles
    {
        get => scene.Styles;
        set => scene.Styles = value;
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
    public void Invalidate() => scene.Invalidate();

    /// <summary>Gets how many windows are open in the process.</summary>
    public static int OpenCount => Open.Count;

    /// <summary>Gives the keyboard focus to a Control of this window, for example the Terminal of a terminal window.</summary>
    /// <param name="control">The Control, or <see langword="null"/> to clear the focus.</param>
    public void Focus(Control? control)
    {
        scene.Focus(control);
    }

    /// <summary>Handles events for every open window until the last one has been closed.</summary>
    /// <remarks>Use it instead of <see cref="Run"/> when an app opens windows next to each other, so that closing one leaves the others open.</remarks>
    public static void RunAll()
    {
        while (Open.Count > 0)
        {
            Open[0].Pump(IdleMilliseconds);
        }
    }

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

    /// <summary>Works out the time between two frames from the refresh rate of the display that a window is on.</summary>
    /// <param name="target">The window.</param>
    /// <returns>The time in milliseconds, between 4 and 33, or the default when the rate is not known.</returns>
    static int FrameTime(SDL_Window* target)
    {
        var mode = SDL3.SDL_GetDesktopDisplayMode(SDL3.SDL_GetDisplayForWindow(target));

        if (mode is null || mode->refresh_rate < 20)
            return Animations.FrameMilliseconds;

        return Math.Clamp((int)Math.Round(1000 / mode->refresh_rate), 4, 33);
    }

    /// <summary>Converts an SDL key code to a <see cref="Key"/>.</summary>
    /// <param name="code">The SDL key code.</param>
    /// <returns>The key, or <see cref="Key.Unknown"/>.</returns>
    static Key ToKey(SDL_Keycode code) => code switch
    {
        SDL_Keycode.SDLK_ESCAPE => Key.Escape,
        SDL_Keycode.SDLK_INSERT => Key.Insert,
        SDL_Keycode.SDLK_F1 => Key.F1,
        SDL_Keycode.SDLK_F2 => Key.F2,
        SDL_Keycode.SDLK_F3 => Key.F3,
        SDL_Keycode.SDLK_F4 => Key.F4,
        SDL_Keycode.SDLK_F6 => Key.F6,
        SDL_Keycode.SDLK_F7 => Key.F7,
        SDL_Keycode.SDLK_F8 => Key.F8,
        SDL_Keycode.SDLK_F9 => Key.F9,
        SDL_Keycode.SDLK_F10 => Key.F10,
        SDL_Keycode.SDLK_F11 => Key.F11,
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

        if (scene.Tick() && !closed)
            Draw();
    }

    /// <summary>Tells how long until a tooltip is due, a toast runs out or an animation needs a frame, so that the event loop wakes up for it.</summary>
    /// <returns>The time in milliseconds, or <see cref="int.MaxValue"/> when nothing is waiting.</returns>
    int DueMilliseconds()
    {
        var due = scene.DueIn();

        return due is { } time ? (int)Math.Ceiling(time.TotalMilliseconds) : int.MaxValue;
    }

    /// <summary>Reacts to one SDL event.</summary>
    /// <param name="e">The event.</param>
    void Handle(SDL_Event e)
    {
        _ = (SDL_EventType)e.type switch
        {
            SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED => Close(),

            SDL_EventType.SDL_EVENT_MOUSE_MOTION => scene.PointerMoved(e.motion.x, e.motion.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == 1 => PressPointer(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == 3 => scene.SecondaryPressed(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when e.button.button == 1 => scene.PointerReleased(e.button.x, e.button.y),
            SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE => scene.PointerLeft(),
            SDL_EventType.SDL_EVENT_MOUSE_WHEEL => scene.Wheel(e.wheel.mouse_x, e.wheel.mouse_y, -e.wheel.y * WheelStep),

            SDL_EventType.SDL_EVENT_KEY_DOWN => PressKey(e.key),
            SDL_EventType.SDL_EVENT_TEXT_INPUT => scene.TextInput(Marshal.PtrToStringUTF8((IntPtr)e.text.text) ?? ""),

            SDL_EventType.SDL_EVENT_WINDOW_EXPOSED or SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED => Refresh(),
            SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED or SDL_EventType.SDL_EVENT_WINDOW_RESTORED or SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED
                => StateChanged(),

            _ => false,
        };
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

    /// <summary>Raises <see cref="KeyPressed"/> for a key press that is not an auto-repeat, and gives the key to the scene.</summary>
    /// <param name="key">The SDL keyboard event.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    bool PressKey(SDL_KeyboardEvent key)
    {
        var pressed = ToKey(key.key);

        if (!key.repeat)
            KeyPressed?.Invoke(pressed);

        return scene.KeyDown(pressed, ToModifiers(key.mod));
    }

    /// <summary>Presses the pointer button and moves the keyboard focus to what was pressed.</summary>
    /// <param name="x">The horizontal position in logical units.</param>
    /// <param name="y">The vertical position in logical units.</param>
    /// <returns><see langword="true"/> when a Pseudo-state changed.</returns>
    bool PressPointer(float x, float y) => scene.PointerPressed(x, y, ToModifiers(SDL3.SDL_GetModState()));

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
        (scene.Root as Window)?.NotifyStateChanged();

        return Refresh();
    }

    /// <summary>Applies the Stylesheets again and asks for a frame, as when the window was exposed or resized.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    bool Refresh()
    {
        scene.Restyle();

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

        var region = WindowHitTester.Test(target.scene.Root, area->x, area->y, width, height, !target.IsMaximized);

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
        int width, height, pixelWidth, pixelHeight;

        SDL3.SDL_GetWindowSize(window, &width, &height);
        SDL3.SDL_GetWindowSizeInPixels(window, &pixelWidth, &pixelHeight);

        if (scene.Render(new Size(width, height)) is not { } list)
            return;

        using var bitmap =
            new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(Background.R, Background.G, Background.B, Background.A));
            canvas.Scale((float)pixelWidth / width, (float)pixelHeight / height);
            list.Replay(new SkiaDrawingContext(canvas));
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

    /// <summary>Gives the time of the window's stopwatch to animated Controls.</summary>
    /// <param name="stopwatch">The window's stopwatch.</param>
    sealed class StopwatchClock(Stopwatch stopwatch) : IAnimationClock
    {
        /// <inheritdoc/>
        public TimeSpan Now => stopwatch.Elapsed;
    }

    /// <summary>Carries out the state changes that a <see cref="Window"/> Control asks for.</summary>
    /// <param name="owner">The native window.</param>
    sealed class Host(SdlWindow owner) : IWindowHost
    {
        /// <inheritdoc/>
        public bool IsMaximized => owner.IsMaximized;

        /// <inheritdoc/>
        public bool AlwaysOnTop
        {
            get => owner.AlwaysOnTop;
            set => owner.AlwaysOnTop = value;
        }

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
