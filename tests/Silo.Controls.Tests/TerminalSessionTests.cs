using System.Text;
using Silo;
using Xunit;

public class TerminalSessionTests
{
    const string Esc = "\u001b";

    sealed class Capture
    {
        readonly StringBuilder text = new();

        public Capture(ITerminalSession session) => session.Output += (_, e) => text.Append(Encoding.UTF8.GetString(e.Data));

        public string Text => text.ToString();
    }

    [Fact]
    public void The_line_editor_inserts_text_and_completes_a_line_with_Enter()
    {
        var editor = new TerminalLineEditor();

        var typed = editor.Feed("ls");

        Assert.Equal("ls", typed.Output);
        Assert.Equal("ls", editor.Text);

        var done = editor.Feed("\r");

        Assert.Equal(["ls"], done.Lines);
        Assert.Equal("\r\n", done.Output);
        Assert.Equal("", editor.Text);
        Assert.Equal(["ls"], editor.History);
    }

    [Fact]
    public void The_line_editor_handles_pasted_text_with_several_lines_and_a_CRLF_once()
    {
        var edit = new TerminalLineEditor().Feed("a\r\nb\nc");

        Assert.Equal(["a", "b"], edit.Lines);
    }

    [Fact]
    public void Backspace_Delete_and_cursor_keys_edit_the_line_in_the_middle()
    {
        var editor = new TerminalLineEditor();

        editor.Feed("abcd");
        editor.Feed(Esc + "[D" + Esc + "[D");

        Assert.Equal(2, editor.Cursor);

        editor.Feed("\u007f");

        Assert.Equal("acd", editor.Text);
        Assert.Equal(1, editor.Cursor);

        editor.Feed(Esc + "[3~");

        Assert.Equal("ad", editor.Text);

        editor.Feed("X");

        Assert.Equal("aXd", editor.Text);
        Assert.Equal(2, editor.Cursor);
    }

    [Fact]
    public void Home_End_and_the_Ctrl_keys_move_and_delete()
    {
        var editor = new TerminalLineEditor();

        editor.Feed("one two");
        editor.Feed("\u0001");

        Assert.Equal(0, editor.Cursor);

        editor.Feed("\u0005");

        Assert.Equal(7, editor.Cursor);

        editor.Feed("\u0017");

        Assert.Equal("one ", editor.Text);

        editor.Feed("\u0001\u000b");

        Assert.Equal("", editor.Text);

        editor.Feed("abc\u0015");

        Assert.Equal("", editor.Text);
    }

    [Fact]
    public void Up_and_Down_walk_through_the_history_and_return_to_the_draft()
    {
        var editor = new TerminalLineEditor();

        editor.Feed("first\rsecond\rdra");
        editor.Feed(Esc + "[A");

        Assert.Equal("second", editor.Text);

        editor.Feed(Esc + "[A");

        Assert.Equal("first", editor.Text);

        editor.Feed(Esc + "[B" + Esc + "[B");

        Assert.Equal("dra", editor.Text);
    }

    [Fact]
    public void Ctrl_C_discards_the_line_and_Ctrl_D_ends_input_only_when_the_line_is_empty()
    {
        var editor = new TerminalLineEditor();

        var interrupted = editor.Feed("abc\u0003");

        Assert.True(interrupted.Interrupted);
        Assert.Contains("^C", interrupted.Output);
        Assert.Equal("", editor.Text);

        Assert.False(editor.Feed("x\u0004").EndOfInput);
        Assert.True(new TerminalLineEditor().Feed("\u0004").EndOfInput);
    }

    [Fact]
    public void Repeated_lines_are_stored_once_and_the_history_has_a_limit()
    {
        var editor = new TerminalLineEditor { HistoryLimit = 2 };

        editor.Feed("a\ra\rb\rc\r");

        Assert.Equal(["b", "c"], editor.History);
    }

    [Fact]
    public void Output_of_the_line_editor_shown_on_a_terminal_matches_the_line()
    {
        var terminal = new Terminal(20, 3);
        var editor = new TerminalLineEditor();

        terminal.Write("> ");
        terminal.Write(editor.Feed("hello").Output);
        terminal.Write(editor.Feed(Esc + "[D" + Esc + "[D" + "XY").Output);

        Assert.Equal("> helXYlo", terminal.Buffer.GetRowText(0).TrimEnd());
        Assert.Equal(7, terminal.Buffer.CursorColumn);
    }

    [Fact]
    public void Split_groups_quoted_words_and_honours_backslashes()
    {
        Assert.Equal(["echo", "a b", "c d", "e f"], CommandSession.Split("echo \"a b\" 'c d' e\\ f"));
        Assert.Equal(["x", ""], CommandSession.Split("x \"\""));
        Assert.Empty(CommandSession.Split("   "));
    }

    [Fact]
    public void A_CommandSession_shows_the_banner_and_prompt_and_runs_a_command()
    {
        var session = new CommandSession { Banner = "Hi\n", Prompt = "> " };
        var capture = new Capture(session);

        session.Add("echo", "Prints", context => context.WriteLine(string.Join(' ', context.Arguments)));
        session.Start(80, 24);

        Assert.Equal("Hi\r\n> ", capture.Text);

        session.Send("echo a  \"b c\"\r");

        Assert.EndsWith("echo a  \"b c\"\r\na b c\r\n> ", capture.Text);
    }

    [Fact]
    public void An_unknown_command_is_reported_and_an_empty_line_just_shows_the_prompt()
    {
        var session = new CommandSession();
        var capture = new Capture(session);

        session.Start(80, 24);
        session.Send("nope\r");

        Assert.Contains("nope: command not found\r\n$ ", capture.Text);

        session.Send("\r");

        Assert.EndsWith("\r\n$ ", capture.Text);
    }

    [Fact]
    public void An_exception_in_a_command_is_printed_in_red_and_the_session_goes_on()
    {
        var session = new CommandSession();
        var capture = new Capture(session);

        session.Add("boom", "Fails", _ => throw new InvalidOperationException("bad"));
        session.Start(80, 24);
        session.Send("boom\r");

        Assert.Contains(Esc + "[31mboom: bad" + Esc + "[0m\r\n$ ", capture.Text);
    }

    [Fact]
    public void The_built_in_help_lists_the_commands_and_exit_ends_the_session()
    {
        var session = new CommandSession();
        var capture = new Capture(session);
        int? exit = null;

        session.Exited += (_, e) => exit = e.ExitCode;
        session.Add("zip", "Packs", _ => { });
        session.Start(80, 24);
        session.Send("help\r");

        Assert.Contains("zip", capture.Text);
        Assert.Contains("Lists the commands", capture.Text);

        session.Send("exit\r");

        Assert.Equal(0, exit);

        var length = capture.Text.Length;

        session.Send("help\r");

        Assert.Equal(length, capture.Text.Length);
    }

    [Fact]
    public async Task Ctrl_C_cancels_a_running_command_and_input_while_it_runs_is_ignored()
    {
        var session = new CommandSession();
        var capture = new Capture(session);
        var started = new TaskCompletionSource();
        var finished = new TaskCompletionSource();

        session.Add("wait", "Waits", async context =>
        {
            started.SetResult();

            try
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }
            finally
            {
                finished.SetResult();
            }
        });

        session.Start(80, 24);
        session.Send("wait\r");

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        session.Send("abc");
        session.Send("\u0003");

        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("^C", capture.Text);
        Assert.EndsWith("$ ", capture.Text);
        Assert.DoesNotContain("abc", capture.Text);
    }

    [Fact]
    public void The_clear_command_and_Ctrl_L_clear_the_screen()
    {
        var session = new CommandSession();
        var capture = new Capture(session);

        session.Start(80, 24);
        session.Send("clear\r");

        Assert.Contains(Esc + "[H" + Esc + "[2J", capture.Text);

        session.Send("ab\u000c");

        Assert.EndsWith(Esc + "[H" + Esc + "[2J$ ab", capture.Text);
    }

    [Fact]
    public void An_attached_CommandSession_draws_on_the_terminal_and_receives_the_keys()
    {
        var terminal = new Terminal(30, 5);
        var session = new CommandSession { Prompt = "$ " };

        session.Add("hi", "Greets", context => context.WriteLine("hello"));
        terminal.Attach(session);

        Assert.Equal("$", terminal.Buffer.GetRowText(0).TrimEnd());

        terminal.Send("hi\r");

        Assert.Equal("$ hi", terminal.Buffer.GetRowText(0).TrimEnd());
        Assert.Equal("hello", terminal.Buffer.GetRowText(1).TrimEnd());
        Assert.Equal("$", terminal.Buffer.GetRowText(2).TrimEnd());
        Assert.Equal(30, session.Columns);
    }

    [Fact]
    public void The_dispatcher_receives_output_and_the_exit_of_a_session()
    {
        var terminal = new Terminal(30, 5);
        var session = new CommandSession();
        var queue = new Queue<Action>();
        var exits = new List<int>();

        terminal.Exited += (_, e) => exits.Add(e.ExitCode);
        terminal.Attach(session, queue.Enqueue);

        Assert.NotEmpty(queue);
        Assert.Equal("", terminal.Buffer.GetRowText(0).TrimEnd());

        while (queue.Count > 0)
            queue.Dequeue()();

        Assert.Equal("$", terminal.Buffer.GetRowText(0).TrimEnd());

        terminal.Send("exit\r");

        while (queue.Count > 0)
            queue.Dequeue()();

        Assert.Equal([0], exits);
    }

    [Fact]
    public void Detach_stops_the_output_and_attaching_another_session_replaces_the_first()
    {
        var terminal = new Terminal(30, 5);
        var first = new CommandSession { Prompt = "1> " };
        var second = new CommandSession { Prompt = "2> " };

        terminal.Attach(first);
        terminal.Attach(second);

        Assert.Same(second, terminal.Session);

        first.Add("x", "x", context => context.Write("late"));
        first.Send("x\r");

        Assert.DoesNotContain("late", terminal.Buffer.GetRowText(0) + terminal.Buffer.GetRowText(1));

        terminal.Detach();

        Assert.Null(terminal.Session);

        second.Add("y", "y", context => context.Write("later"));
        second.Send("y\r");

        Assert.DoesNotContain("later", terminal.Buffer.GetRowText(0) + terminal.Buffer.GetRowText(1));
    }

    [Fact]
    public void Resizing_the_terminal_tells_the_session()
    {
        var terminal = new Terminal(30, 5);
        var session = new CommandSession();

        terminal.Attach(session);
        terminal.Buffer.Resize(40, 10);

        var (columns, rows) = (session.Columns, session.Rows);

        Assert.Equal((30, 5), (columns, rows));
    }

    [Fact]
    public async Task A_ProcessSession_runs_a_program_and_reports_its_exit_code()
    {
        var (file, args) = OperatingSystem.IsWindows() ? ("cmd", new[] { "/c", "echo hello" }) : ("sh", ["-c", "echo hello; exit 3"]);

        using var session = new ProcessSession(file, args);
        var capture = new Capture(session);
        var exited = new TaskCompletionSource<int>();

        session.Exited += (_, e) => exited.TrySetResult(e.ExitCode);
        session.Start(80, 24);

        var code = await exited.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Contains("hello\r\n", capture.Text);

        if (!OperatingSystem.IsWindows())
            Assert.Equal(3, code);
    }

    [Fact]
    public async Task A_ProcessSession_sends_the_edited_lines_to_the_program_and_echoes_them()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var session = new ProcessSession("cat");
        var capture = new Capture(session);
        var exited = new TaskCompletionSource<int>();

        session.Exited += (_, e) => exited.TrySetResult(e.ExitCode);
        session.Start(80, 24);
        session.Send("abx\u007fc\r");
        session.Send("\u0004");

        await exited.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        var terminal = new Terminal(20, 5);

        terminal.Write(capture.Text);

        Assert.Equal("abc", terminal.Buffer.GetRowText(0).TrimEnd());
        Assert.Equal("abc", terminal.Buffer.GetRowText(1).TrimEnd());
    }

    [Fact]
    public async Task A_missing_program_prints_an_error_and_exits_with_127()
    {
        using var session = new ProcessSession("silo-no-such-program-xyz");
        var capture = new Capture(session);
        var exited = new TaskCompletionSource<int>();

        session.Exited += (_, e) => exited.TrySetResult(e.ExitCode);
        session.Start(80, 24);

        Assert.Equal(127, await exited.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
        Assert.Contains(Esc + "[31m", capture.Text);
    }
}
