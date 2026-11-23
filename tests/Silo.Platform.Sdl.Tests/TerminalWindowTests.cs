using SDL;
using Silo;
using Silo.Platform.Sdl;
using Silo.Styling;
using Xunit;

/// <summary>Opens real terminal windows on the dummy video driver, so no display is needed.</summary>
[Collection("sdl")]
public class TerminalWindowTests
{
    static StyleResolver Styles() => new([DefaultTheme.Load()]);

    static CommandSession NewSession()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        var session = new CommandSession { Prompt = "$ ", Banner = "Welcome\n" };

        session.Add("hi", "Greets", context => context.WriteLine("hello " + string.Join(' ', context.Arguments)));

        session.Add("bg", "Writes from a thread", async context => await Task.Run(() => context.WriteLine("from a thread")));
        session.Add("title", "Sets the title", context => context.Write("\u001b]0;build\u0007"));

        return session;
    }

    [Fact]
    public void The_window_opens_at_the_size_of_the_columns_and_rows_and_shows_the_banner()
    {
        using var terminal = new TerminalWindow(NewSession(), "Console", 60, 10, Styles());

        terminal.Window.Pump(0);
        terminal.Window.Pump(0);

        Assert.Equal((60, 10), (terminal.Terminal.Buffer.Columns, terminal.Terminal.Buffer.Rows));
        Assert.Equal("Welcome", terminal.Terminal.Buffer.GetRowText(0).TrimEnd());
        Assert.Equal("$", terminal.Terminal.Buffer.GetRowText(1).TrimEnd());
    }

    [Fact]
    public void Typed_text_and_Enter_reach_the_session_because_the_terminal_has_the_focus()
    {
        using var terminal = new TerminalWindow(NewSession(), "Console", 40, 8, Styles());

        terminal.Window.Pump(0);

        Events.PushText("hi there", terminal.Window.Id);
        Events.PushKey(SDL_Keycode.SDLK_RETURN, terminal.Window.Id, repeat: false);
        terminal.Window.Pump(100);
        terminal.Window.Pump(100);

        Assert.Equal("$ hi there", terminal.Terminal.Buffer.GetRowText(1).TrimEnd());
        Assert.Equal("hello there", terminal.Terminal.Buffer.GetRowText(2).TrimEnd());
    }

    [Fact]
    public void Output_from_another_thread_is_drawn_on_the_window_thread()
    {
        var session = NewSession();

        using var terminal = new TerminalWindow(session, "Console", 40, 8, Styles());

        terminal.Window.Pump(0);

        session.Send("bg\r");

        for (var i = 0; i < 20 && !Enumerable.Range(0, 8).Any(row => terminal.Terminal.Buffer.GetRowText(row).StartsWith("from a thread")); i++)
            terminal.Window.Pump(50);

        Assert.Contains(Enumerable.Range(0, 8).Select(terminal.Terminal.Buffer.GetRowText), row => row.StartsWith("from a thread"));
    }

    [Fact]
    public void The_title_of_the_window_follows_the_title_the_program_sets()
    {
        var session = NewSession();

        using var terminal = new TerminalWindow(session, "Console", 40, 8, Styles());
        var titles = new List<string>();

        terminal.Terminal.TitleChanged += (_, _) => titles.Add(terminal.Terminal.Buffer.Title);
        session.Send("title\r");
        terminal.Window.Pump(100);

        Assert.Equal(["build"], titles);
    }

    [Fact]
    public void The_window_closes_when_the_session_ends_and_stays_open_without_CloseOnExit()
    {
        using var closing = new TerminalWindow(NewSession(), "Console", 40, 8, Styles());
        using var staying = new TerminalWindow(NewSession(), "Console", 40, 8, Styles()) { CloseOnExit = false };
        var closed = new List<string>();

        closing.Window.Closed += (_, _) => closed.Add("closing");
        staying.Window.Closed += (_, _) => closed.Add("staying");

        closing.Window.Pump(0);

        closing.Session.Send("exit\r");
        closing.Window.Pump(100);
        staying.Session.Send("exit\r");
        closing.Window.Pump(100);

        Assert.Equal(["closing"], closed);
    }

    [Fact]
    public void Closing_the_window_disposes_the_session()
    {
        var session = NewSession();
        var terminal = new TerminalWindow(session, "Console", 40, 8, Styles());

        terminal.Window.Pump(0);

        Events.PushClose(terminal.Window.Id);

        Assert.False(terminal.Window.Pump(100));

        session.Send("hi\r");

        Assert.DoesNotContain("hello", terminal.Terminal.Buffer.GetRowText(2));
    }

    [Fact]
    public void RunAll_keeps_going_until_the_last_window_is_closed()
    {
        var before = SdlWindow.OpenCount;
        using var first = new TerminalWindow(NewSession(), "One", 30, 5, Styles());
        using var second = new TerminalWindow(NewSession(), "Two", 30, 5, Styles());

        Assert.Equal(before + 2, SdlWindow.OpenCount);

        first.Window.Pump(0);

        Events.PushClose(first.Window.Id);
        first.Window.Pump(100);

        Assert.Equal(before + 1, SdlWindow.OpenCount);

        Events.PushClose(second.Window.Id);

        SdlWindow.RunAll();

        Assert.Equal(before, SdlWindow.OpenCount);
    }
}
