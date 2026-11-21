using SDL;
using Silo;
using Silo.Platform.Sdl;
using Silo.Styling;
using Xunit;

/// <summary>Runs a real SDL window on the dummy video driver, so no display is needed.</summary>
public class SdlWindowTests
{
    static (SdlWindow Window, Button Button) Setup()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        var button = new Button { Text = "Go" };
        var root = new Box { Name = "root" };

        root.Children.Add(button);

        var window = new SdlWindow("test", 200, 100, root) { Styles = new StyleResolver([DefaultTheme.Load()]) };

        return (window, button);
    }

    static void Mouse(SDL_EventType type, float x, float y, uint windowId = 0)
    {
        SDL_Event e = default;

        e.type = (uint)type;
        e.motion.windowID = (SDL_WindowID)windowId;
        e.button.windowID = (SDL_WindowID)windowId;
        e.motion.x = x;
        e.motion.y = y;
        e.button.button = 1;
        e.button.x = x;
        e.button.y = y;

        Events.Push(e);
    }

    [Fact]
    public void The_first_pump_draws_a_frame()
    {
        var (window, _) = Setup();

        using (window)
        {
            Assert.True(window.Pump(0));
            Assert.Equal(1, window.Frames);
        }
    }

    [Fact]
    public void Pointer_events_click_the_button()
    {
        var (window, button) = Setup();

        using (window)
        {
            var clicks = 0;

            button.Click += (_, _) => clicks++;

            window.Pump(0);

            Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN, 5, 5);
            window.Pump(100);
            Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP, 5, 5);
            window.Pump(100);

            Assert.Equal(1, clicks);
        }
    }

    [Fact]
    public void Events_go_to_the_window_they_name_when_several_are_open()
    {
        var (first, firstButton) = Setup();
        var (second, secondButton) = Setup();

        using (first)
        using (second)
        {
            var clicks = new List<string>();

            firstButton.Click += (_, _) => clicks.Add("first");
            secondButton.Click += (_, _) => clicks.Add("second");

            first.Pump(0);

            Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN, 5, 5, second.Id);
            first.Pump(100);
            Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP, 5, 5, second.Id);
            first.Pump(100);

            Assert.Equal(["second"], clicks);
        }
    }

    [Fact]
    public void Keys_are_reported_to_the_window_that_has_them_and_auto_repeat_is_ignored()
    {
        var (window, _) = Setup();

        using (window)
        {
            var keys = new List<Key>();

            window.KeyPressed += keys.Add;

            Events.PushKey(SDL_Keycode.SDLK_F12, window.Id, repeat: false);
            Events.PushKey(SDL_Keycode.SDLK_F12, window.Id, repeat: true);
            Events.PushKey(SDL_Keycode.SDLK_SPACE, window.Id, repeat: false);
            window.Pump(100);

            Assert.Equal([Key.F12, Key.Unknown], keys);
        }
    }

    [Fact]
    public void Closing_one_window_raises_Closed_and_leaves_the_others_running()
    {
        var (first, _) = Setup();
        var (second, _) = Setup();

        using (first)
        using (second)
        {
            var closed = 0;

            second.Closed += (_, _) => closed++;

            Events.PushClose(second.Id);

            Assert.True(first.Pump(100));
            Assert.Equal(1, closed);
            Assert.False(second.Pump(0));
        }
    }

    [Fact]
    public async Task Posted_work_runs_on_the_pumping_thread_and_a_quit_event_ends_the_loop()
    {
        var (window, _) = Setup();

        using (window)
        {
            var ran = false;

            await Task.Run(() => window.Post(() => ran = true), TestContext.Current.CancellationToken);
            window.Pump(100);

            Assert.True(ran);

            Events.Push(new SDL_Event { type = (uint)SDL_EventType.SDL_EVENT_QUIT });

            Assert.False(window.Pump(100));
        }
    }
}

/// <summary>Pushes events onto the SDL queue.</summary>
static unsafe class Events
{
    /// <summary>Pushes one event.</summary>
    /// <param name="e">The event.</param>
    public static void Push(SDL_Event e) => SDL3.SDL_PushEvent(&e);

    /// <summary>Pushes a key press.</summary>
    /// <param name="code">The key.</param>
    /// <param name="windowId">The window that has the keyboard focus.</param>
    /// <param name="repeat">Whether the event is an auto-repeat.</param>
    public static void PushKey(SDL_Keycode code, uint windowId, bool repeat)
    {
        SDL_Event e = default;

        e.type = (uint)SDL_EventType.SDL_EVENT_KEY_DOWN;
        e.key.key = code;
        e.key.windowID = (SDL_WindowID)windowId;
        e.key.down = true;
        e.key.repeat = repeat;

        Push(e);
    }

    /// <summary>Pushes a request to close a window.</summary>
    /// <param name="windowId">The window.</param>
    public static void PushClose(uint windowId)
    {
        SDL_Event e = default;

        e.type = (uint)SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED;
        e.window.windowID = (SDL_WindowID)windowId;

        Push(e);
    }
}
