using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;

namespace Silo;

/// <summary>
/// Everything that runs a window's content except the native window: the root Control, its overlays, the pointer and keyboard
/// rules, tooltips, toasts, animations and the steps of a frame. A platform window feeds it input and draws what it renders.
/// </summary>
/// <remarks>
/// <para>
/// Input goes in as plain calls in window coordinates (<see cref="PointerMoved"/>, <see cref="KeyDown"/> and so on). Each call
/// that changed something applies the Stylesheets again and marks the scene dirty, so the platform only has to ask
/// <see cref="IsDirty"/> and call <see cref="Render"/>.
/// </para>
/// <para>
/// Time comes from the <see cref="IAnimationClock"/> given to the constructor, so a test can move it by hand with a
/// <see cref="ManualAnimationClock"/> and call <see cref="Tick"/>. <see cref="DueIn"/> tells the event loop how long it may
/// sleep.
/// </para>
/// <para>
/// The scene sets <see cref="Control.Overlays"/> on the root and gives the root the clock when it has none. The root needs a
/// <see cref="Control.TextMeasurer"/> before the scene is created, since the overlays measure text with the same one.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var clock = new ManualAnimationClock();
/// var scene = new WindowScene(root, clock) { Styles = styles };
///
/// scene.PointerMoved(40, 20);
/// scene.PointerPressed(40, 20, KeyModifiers.None);
///
/// var list = scene.Render(new Size(800, 600));
/// </code>
/// </example>
public sealed class WindowScene
{
    /// <summary>The Control at the root of the window's content.</summary>
    readonly Control root;

    /// <summary>The clock that tooltips, toasts and animations read.</summary>
    readonly IAnimationClock clock;

    /// <summary>The overlays that are drawn above the root.</summary>
    readonly OverlayLayer overlays;

    /// <summary>Turns pointer input into hover, presses and wheel turns.</summary>
    readonly PointerRouter router;

    /// <summary>Holds the Control that gets the keyboard.</summary>
    readonly FocusManager focus = new();

    /// <summary>Shows and hides the tooltips.</summary>
    readonly TooltipPresenter tooltips;

    /// <summary>Moves the Controls that animate.</summary>
    readonly AnimationScheduler scheduler;


    /// <summary>The Stylesheets, or <see langword="null"/> for none.</summary>
    StyleResolver? styles;

    /// <summary>Whether a new frame is needed.</summary>
    bool dirty = true;

    /// <summary>Whether something was animating when the last frame was rendered.</summary>
    bool animating;


    /// <summary>Creates a scene around a root Control.</summary>
    /// <param name="root">The root of the window's content. Its <see cref="Control.TextMeasurer"/> must be set.</param>
    /// <param name="clock">The clock that tooltips, toasts and animations read.</param>
    /// <param name="frameMilliseconds">The time between two frames of an animation, in milliseconds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> or <paramref name="clock"/> is <see langword="null"/>.</exception>
    public WindowScene(Control root, IAnimationClock clock, int frameMilliseconds = Animations.FrameMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(clock);

        this.root = root;
        this.clock = clock;

        root.AnimationClock ??= clock;
        overlays = new OverlayLayer { TextMeasurer = root.TextMeasurer, AnimationClock = clock };
        root.Overlays = overlays;
        tooltips = new TooltipPresenter(overlays);
        Toasts = new ToastHost(overlays);
        overlays.Changed += (_, _) => Restyle();
        router = new PointerRouter(root, overlays);
        scheduler = new AnimationScheduler(root, overlays) { FrameMilliseconds = frameMilliseconds };
    }


    /// <summary>Gets the Control at the root of the window's content.</summary>
    public Control Root => root;

    /// <summary>Gets the overlays that are drawn above the root, such as menus, dialogs and tooltips.</summary>
    public OverlayLayer Overlays => overlays;

    /// <summary>Gets the toasts of the window.</summary>
    public ToastHost Toasts { get; }

    /// <summary>Gets whether a new frame is needed.</summary>
    public bool IsDirty => dirty;

    /// <summary>Gets or sets the Stylesheets of the root and the overlays. Setting them applies them.</summary>
    public StyleResolver? Styles
    {
        get => styles;
        set
        {
            styles = value;

            Restyle();
        }
    }

    /// <summary>Gets or sets whether animations are skipped.</summary>
    public bool ReducedMotion
    {
        get => scheduler.ReducedMotion;
        set => scheduler.ReducedMotion = value;
    }


    /// <summary>Applies the Stylesheets to the root and the overlays, and asks for a frame.</summary>
    public void Restyle()
    {
        if (styles is not null)
        {
            root.ApplyStyles(styles);
            overlays.ApplyStyles(styles);
        }

        dirty = true;
    }

    /// <summary>Asks for a new frame without applying the Stylesheets.</summary>
    public void Invalidate() => dirty = true;

    /// <summary>Gives the keyboard to a Control.</summary>
    /// <param name="control">The Control, or <see langword="null"/> to take the keyboard away.</param>
    public void Focus(Control? control)
    {
        if (focus.Focus(control))
            dirty = true;
    }

    /// <summary>Moves the pointer.</summary>
    /// <param name="x">The horizontal position in the window.</param>
    /// <param name="y">The vertical position in the window.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool PointerMoved(double x, double y) => Apply(router.Move(x, y) | tooltips.Hover(router.Hovered, x, y, clock.Now));

    /// <summary>Presses the primary pointer button, which also moves the keyboard to what was pressed.</summary>
    /// <param name="x">The horizontal position in the window.</param>
    /// <param name="y">The vertical position in the window.</param>
    /// <param name="modifiers">The modifier keys that are held.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    /// <remarks>While a modal overlay is shown, the press does not move the keyboard.</remarks>
    public bool PointerPressed(double x, double y, KeyModifiers modifiers)
    {
        var overlaid = overlays.ModalCount > 0;

        router.Modifiers = modifiers;

        var changed = router.Down(x, y) | tooltips.Dismiss();

        return Apply((!overlaid && focus.FocusFrom(router.Hovered)) | changed);
    }

    /// <summary>Presses the secondary pointer button, which opens the context menu under the pointer.</summary>
    /// <param name="x">The horizontal position in the window.</param>
    /// <param name="y">The vertical position in the window.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool SecondaryPressed(double x, double y) => Apply(router.SecondaryDown(x, y));

    /// <summary>Releases the primary pointer button.</summary>
    /// <param name="x">The horizontal position in the window.</param>
    /// <param name="y">The vertical position in the window.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool PointerReleased(double x, double y) => Apply(router.Up(x, y));

    /// <summary>Tells the scene that the pointer left the window.</summary>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool PointerLeft() => Apply(router.Leave() | tooltips.Dismiss());

    /// <summary>Turns the mouse wheel over a point.</summary>
    /// <param name="x">The horizontal position in the window.</param>
    /// <param name="y">The vertical position in the window.</param>
    /// <param name="delta">The distance to scroll in pixels. Positive scrolls down.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool Wheel(double x, double y, double delta) => Apply(tooltips.Dismiss() | router.Scroll(x, y, delta));

    /// <summary>Presses a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys that are held.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    /// <remarks>
    /// Escape closes the top overlay first. Then a menu's access key gets it. While a modal overlay is shown it takes the key,
    /// and otherwise the Control that has the keyboard does.
    /// </remarks>
    public bool KeyDown(Key key, KeyModifiers modifiers)
    {
        var hidden = tooltips.Dismiss();

        if (key == Key.Escape && overlays.CloseTop())
            return Apply(true);

        if (MenuBar.AccessKeyDown(root, key, modifiers))
            return Apply(true);

        if (overlays.ModalCount > 0)
            return Apply(overlays.KeyDown(key, modifiers) | hidden);

        return Apply(focus.KeyDown(key, modifiers) | hidden);
    }

    /// <summary>Types text into the Control that has the keyboard.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool TextInput(string text) => Apply(focus.TextInput(text));

    /// <summary>Lets time pass: tooltips come and go, toasts run out, and animations move.</summary>
    /// <returns><see langword="true"/> when a frame is needed.</returns>
    public bool Tick()
    {
        var now = clock.Now;

        if (tooltips.Tick(now) | Toasts.Tick(now))
            dirty = true;

        if (animating)
            dirty = true;

        if (animating && scheduler.Tick())
            Restyle();

        return dirty;
    }

    /// <summary>Tells how long until a tooltip is due, a toast runs out or the next frame of an animation, so that the event loop can sleep that long.</summary>
    /// <returns>The time, or <see langword="null"/> when nothing is waiting.</returns>
    public TimeSpan? DueIn()
    {
        var tooltip = tooltips.DueIn(clock.Now);
        var toast = Toasts.DueIn();
        var due = tooltip is null ? toast : toast is null ? tooltip : tooltip < toast ? tooltip : toast;

        if (!animating)
            return due;

        var frame = TimeSpan.FromMilliseconds(scheduler.FrameMilliseconds);

        return due is { } wait && wait < frame ? wait : frame;
    }

    /// <summary>Arranges the root and the overlays and renders them into a display list.</summary>
    /// <param name="size">The size of the window.</param>
    /// <returns>The display list, or <see langword="null"/> when the window has no area, as when it is minimized.</returns>
    /// <remarks>It clears <see cref="IsDirty"/> either way.</remarks>
    public DisplayList? Render(Size size)
    {
        dirty = false;

        if (size.Width <= 0 || size.Height <= 0)
            return null;

        root.Arrange(size);

        var builder = new DisplayListBuilder();

        root.Render(builder);

        overlays.Arrange(size);
        overlays.Render(builder);

        animating = scheduler.IsAnimating;

        return builder.Build();
    }

    /// <summary>Applies the Stylesheets again when something changed.</summary>
    /// <param name="changed">Whether something changed.</param>
    /// <returns><paramref name="changed"/>.</returns>
    bool Apply(bool changed)
    {
        if (changed)
            Restyle();

        return changed;
    }
}
