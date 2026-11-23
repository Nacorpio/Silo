using System.Runtime.InteropServices;
using SDL;
using Silo;
using Silo.Platform.Sdl;
using Silo.Styling;
using Xunit;

/// <summary>Runs a real SDL window on the dummy video driver, so no display is needed.</summary>
[Collection("sdl")]
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
    public void Clicking_a_text_box_focuses_it_and_typed_text_and_keys_edit_it()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        var box = new TextBox();
        var root = new Box { Name = "root" };

        root.Children.Add(box);

        using var window = new SdlWindow("test", 200, 100, root) { Styles = new StyleResolver([DefaultTheme.Load()]) };

        window.Pump(0);

        var bounds = box.Bounds;
        var x = (float)(bounds.X + 5);
        var y = (float)(bounds.Y + 5);

        Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN, x, y, window.Id);
        Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP, x, y, window.Id);
        Events.PushText("héllo", window.Id);
        Events.PushKey(SDL_Keycode.SDLK_BACKSPACE, window.Id, repeat: false);
        Events.PushKey(SDL_Keycode.SDLK_BACKSPACE, window.Id, repeat: true);
        window.Pump(100);

        Assert.Equal("hél", box.Text);
        Assert.Contains("focus", box.PseudoStates);
    }

    [Fact]
    public void An_overlay_is_drawn_and_closed_by_Escape_or_a_press_outside()
    {
        var (window, _) = Setup();

        using (window)
        {
            var menu = new Box { Name = "menu" };

            window.Pump(0);
            window.Overlays.Show(menu, 100, 50);
            window.Pump(0);

            Assert.Equal((100, 50), (menu.Bounds.X, menu.Bounds.Y));

            Events.PushKey(SDL_Keycode.SDLK_ESCAPE, window.Id, repeat: false);
            window.Pump(0);

            Assert.False(window.Overlays.Contains(menu));

            window.Overlays.Show(menu, 100, 50);
            window.Pump(0);
            Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN, 5, 90, window.Id);
            window.Pump(0);

            Assert.False(window.Overlays.Contains(menu));
        }
    }

    [Fact]
    public void A_tooltip_shows_after_the_pointer_rests_on_a_Control_and_a_press_hides_it()
    {
        var (window, button) = Setup();

        using (window)
        {
            button.Tooltip = "Go somewhere";
            window.Pump(0);

            var x = (float)(button.Bounds.X + 5);
            var y = (float)(button.Bounds.Y + 5);

            Mouse(SDL_EventType.SDL_EVENT_MOUSE_MOTION, x, y, window.Id);
            window.Pump(0);

            Assert.Equal(0, window.Overlays.Count);

            window.Pump(700);

            Assert.Equal(1, window.Overlays.Count);
            Assert.Equal(0, window.Overlays.ModalCount);

            Mouse(SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN, x, y, window.Id);
            window.Pump(0);

            Assert.Equal(0, window.Overlays.Count);
        }
    }

    [Fact]
    public void A_window_made_from_a_Window_follows_its_title_without_failing_after_it_is_closed()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        var content = new Window { Title = "One", InitialWidth = 320, InitialHeight = 200 };
        var window = new SdlWindow(content);

        using (window)
        {
            content.Title = "Two";

            Assert.True(window.Pump(0));
        }

        content.Title = "Three";
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
    public void A_window_can_be_shown_above_all_others_and_a_Window_Control_hands_its_value_on_when_it_opens()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        var content = new Window { Title = "top", AlwaysOnTop = true };

        using var window = new SdlWindow(content);

        Assert.True(window.AlwaysOnTop);

        content.AlwaysOnTop = false;

        Assert.False(window.AlwaysOnTop);

        window.AlwaysOnTop = true;

        Assert.True(content.AlwaysOnTop);
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

    /// <summary>Pushes typed text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="windowId">The window that has the keyboard focus.</param>
    public static void PushText(string text, uint windowId)
    {
        SDL_Event e = default;

        e.type = (uint)SDL_EventType.SDL_EVENT_TEXT_INPUT;
        e.text.windowID = (SDL_WindowID)windowId;
        e.text.text = (byte*)Marshal.StringToCoTaskMemUTF8(text);

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
