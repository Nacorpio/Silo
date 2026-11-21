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

    static void Mouse(SDL_EventType type, float x, float y)
    {
        SDL_Event e = default;

        e.type = (uint)type;
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
}
