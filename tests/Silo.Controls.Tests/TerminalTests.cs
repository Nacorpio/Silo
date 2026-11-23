using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class TerminalTests
{
    const string Esc = "\u001b";

    const string Sheet = """
        { "rules": { "Terminal": { "width": "296px", "height": "174px", "padding": "8px" } } }
        """;

    sealed class FakeClipboard : IClipboard
    {
        public string Text { get; set; } = string.Empty;

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    sealed class FakeClock : IAnimationClock
    {
        public TimeSpan Now { get; set; }
    }

    static (Terminal Terminal, Box Root) Setup(string sheet = Sheet)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var terminal = new Terminal();

        root.Children.Add(terminal);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        return (terminal, root);
    }

    static List<DrawCommand> Render(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands];
    }

    static List<string> Typed(Terminal terminal)
    {
        var typed = new List<string>();

        terminal.Input += (_, e) => typed.Add(e.Text);

        return typed;
    }

    [Fact]
    public void The_grid_follows_the_size_of_the_control_and_the_program_is_told()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var terminal = new Terminal();
        var resized = new List<(int, int)>();

        terminal.Resized += (_, e) => resized.Add((e.Columns, e.Rows));
        root.Children.Add(terminal);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        Assert.Equal((40, 9), (terminal.Columns, terminal.Rows));
        Assert.Equal([(40, 9)], resized);

        root.Arrange(new Size(400, 300));

        Assert.Single(resized);
    }

    [Fact]
    public void Without_a_size_the_natural_size_is_the_grid_of_cells()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var terminal = new Terminal(10, 4);

        root.Children.Add(terminal);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "Box": { "align-items": "flex-start" } } }""", "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        Assert.Equal(70, terminal.Bounds.Width);
        Assert.Equal(68, terminal.Bounds.Height);
        Assert.Equal((10, 4), (terminal.Columns, terminal.Rows));
    }

    [Fact]
    public void Written_output_is_drawn_as_text()
    {
        var (terminal, root) = Setup();

        terminal.Write("hello world\r\n");

        var commands = Render(root);

        Assert.Contains(commands, c => c is DrawTextCommand { Text: "hello world" });
    }

    [Fact]
    public void ANSI_colors_use_the_palette_and_bold_brightens_the_first_eight()
    {
        var (terminal, root) = Setup();

        terminal.Write($"{Esc}[31mred{Esc}[1;31mBOLD{Esc}[0m");

        var commands = Render(root).OfType<DrawTextCommand>().ToList();

        Assert.Contains(commands, c => c.Text == "red" && c.Color == new Silo.Styling.Values.Color(205, 49, 49));
        Assert.Contains(commands, c => c.Text == "BOLD" && c.Color == new Silo.Styling.Values.Color(241, 76, 76) && c.Style.Bold);
    }

    [Fact]
    public void Colors_of_the_256_palette_and_true_color_are_resolved()
    {
        var (terminal, root) = Setup();

        terminal.Write($"{Esc}[38;5;196mA{Esc}[38;5;244mB{Esc}[38;2;1;2;3mC");

        var commands = Render(root).OfType<DrawTextCommand>().ToList();

        Assert.Contains(commands, c => c.Text == "A" && c.Color == new Silo.Styling.Values.Color(255, 0, 0));
        Assert.Contains(commands, c => c.Text == "B" && c.Color == new Silo.Styling.Values.Color(128, 128, 128));
        Assert.Contains(commands, c => c.Text == "C" && c.Color == new Silo.Styling.Values.Color(1, 2, 3));
    }

    [Fact]
    public void A_cell_background_is_filled_and_reverse_swaps_the_colors()
    {
        var (terminal, root) = Setup();

        terminal.Write($"{Esc}[44mab{Esc}[0m{Esc}[7mcd");

        var commands = Render(root);

        Assert.Contains(commands, c => c is FillRectCommand { Color: { R: 36, G: 114, B: 200 }, Rect.Width: 14 });
        Assert.Contains(commands, c => c is FillRectCommand { Color: { R: 212, G: 212, B: 212 }, Rect.Width: 14 });
        Assert.Contains(commands, c => c is DrawTextCommand { Text: "cd", Color: { R: 12, G: 15, B: 26 } });
    }

    [Fact]
    public void Typed_text_goes_to_the_Input_event_without_control_characters()
    {
        var (terminal, _) = Setup();
        var typed = Typed(terminal);

        Assert.True(terminal.TextInput("ls"));
        Assert.False(terminal.TextInput("\u0001"));

        Assert.Equal(["ls"], typed);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None, false, "\r")]
    [InlineData(Key.Backspace, KeyModifiers.None, false, "\u007f")]
    [InlineData(Key.Tab, KeyModifiers.None, false, "\t")]
    [InlineData(Key.Tab, KeyModifiers.Shift, false, "\u001b[Z")]
    [InlineData(Key.Escape, KeyModifiers.None, false, "\u001b")]
    [InlineData(Key.Up, KeyModifiers.None, false, "\u001b[A")]
    [InlineData(Key.Up, KeyModifiers.None, true, "\u001bOA")]
    [InlineData(Key.Left, KeyModifiers.Control, false, "\u001b[1;5D")]
    [InlineData(Key.Right, KeyModifiers.Shift | KeyModifiers.Alt, true, "\u001b[1;4C")]
    [InlineData(Key.Home, KeyModifiers.None, false, "\u001b[H")]
    [InlineData(Key.End, KeyModifiers.None, true, "\u001bOF")]
    [InlineData(Key.Delete, KeyModifiers.None, false, "\u001b[3~")]
    [InlineData(Key.Insert, KeyModifiers.None, false, "\u001b[2~")]
    [InlineData(Key.PageUp, KeyModifiers.None, false, "\u001b[5~")]
    [InlineData(Key.PageDown, KeyModifiers.Control, false, "\u001b[6;5~")]
    [InlineData(Key.F1, KeyModifiers.None, false, "\u001bOP")]
    [InlineData(Key.F4, KeyModifiers.Shift, false, "\u001b[1;2S")]
    [InlineData(Key.F5, KeyModifiers.None, false, "\u001b[15~")]
    [InlineData(Key.F12, KeyModifiers.None, false, "\u001b[24~")]
    [InlineData(Key.C, KeyModifiers.Control, false, "\u0003")]
    [InlineData(Key.D, KeyModifiers.Control, false, "\u0004")]
    [InlineData(Key.A, KeyModifiers.Control | KeyModifiers.Alt, false, "\u001b\u0001")]
    [InlineData(Key.B, KeyModifiers.Alt, false, "\u001bb")]
    [InlineData(Key.B, KeyModifiers.Alt | KeyModifiers.Shift, false, "\u001bB")]
    [InlineData(Key.Enter, KeyModifiers.Alt, false, "\u001b\r")]
    [InlineData(Key.B, KeyModifiers.None, false, null)]
    public void Keys_are_encoded_as_a_program_expects(Key key, KeyModifiers modifiers, bool application, string? expected) =>
        Assert.Equal(expected, Terminal.Encode(key, modifiers, application));

    [Fact]
    public void Keys_go_to_Input_and_arrows_follow_the_application_mode_of_the_program()
    {
        var (terminal, _) = Setup();
        var typed = Typed(terminal);

        terminal.KeyDown(Key.Up, KeyModifiers.None);
        terminal.Write($"{Esc}[?1h");
        terminal.KeyDown(Key.Up, KeyModifiers.None);
        terminal.KeyDown(Key.C, KeyModifiers.Control);

        Assert.Equal([$"{Esc}[A", $"{Esc}OA", "\u0003"], typed);
    }

    [Fact]
    public void Replies_of_the_parser_go_to_Input()
    {
        var (terminal, _) = Setup();
        var typed = Typed(terminal);

        terminal.Write($"{Esc}[2;3H{Esc}[6n");

        Assert.Equal([$"{Esc}[2;3R"], typed);
    }

    [Fact]
    public void Dragging_selects_text_and_copy_sends_it_to_the_clipboard()
    {
        var (terminal, _) = Setup();
        var clipboard = new FakeClipboard();

        terminal.Clipboard = clipboard;
        terminal.Write("hello world\r\nsecond line");

        terminal.PointerDown(8 + 0, 8 + 2);
        terminal.PointerDrag(8 + (4 * 7) + 1, 8 + 2);
        terminal.PointerUp();

        Assert.True(terminal.HasSelection);
        Assert.Equal("hello", terminal.SelectedText);
        Assert.True(terminal.KeyDown(Key.C, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Equal("hello", clipboard.Text);
    }

    [Fact]
    public void A_selection_over_several_lines_joins_them_and_drops_trailing_spaces()
    {
        var (terminal, _) = Setup();

        terminal.Write("one   \r\ntwo");
        terminal.SelectAll();

        Assert.Equal("one\ntwo", terminal.SelectedText.TrimEnd('\n'));
    }

    [Fact]
    public void A_click_without_dragging_clears_the_selection()
    {
        var (terminal, _) = Setup();

        terminal.Write("hello");
        terminal.SelectAll();
        terminal.PointerDown(10, 10);
        terminal.PointerUp();

        Assert.False(terminal.HasSelection);
        Assert.Equal(string.Empty, terminal.SelectedText);
    }

    [Fact]
    public void A_double_press_selects_the_word()
    {
        var (terminal, _) = Setup();
        var now = 1000L;

        terminal.Clock = () => now;
        terminal.Write("run  /usr/bin/ls -la");

        terminal.PointerDown(8 + (10 * 7), 10);
        terminal.PointerUp();

        now += 200;

        terminal.PointerDown(8 + (10 * 7), 10);
        terminal.PointerUp();

        Assert.Equal("/usr/bin/ls", terminal.SelectedText);

        now += 5000;

        terminal.PointerDown(8 + (10 * 7), 10);

        Assert.False(terminal.HasSelection);
    }

    [Fact]
    public void Paste_sends_the_clipboard_with_carriage_returns_and_bracket_markers_when_asked()
    {
        var (terminal, _) = Setup();
        var typed = Typed(terminal);

        terminal.Clipboard = new FakeClipboard { Text = "a\r\nb\nc" };

        Assert.True(terminal.KeyDown(Key.V, KeyModifiers.Control | KeyModifiers.Shift));

        terminal.Write($"{Esc}[?2004h");

        Assert.True(terminal.KeyDown(Key.Insert, KeyModifiers.Shift));
        Assert.Equal(["a\rb\rc", $"{Esc}[200~a\rb\rc{Esc}[201~"], typed);
    }

    [Fact]
    public void Paste_and_copy_do_nothing_without_a_clipboard()
    {
        var (terminal, _) = Setup();

        Assert.False(terminal.Paste());
        Assert.False(terminal.Copy());
    }

    [Fact]
    public void The_view_scrolls_through_the_scrollback_and_typing_returns_to_the_bottom()
    {
        var (terminal, root) = Setup();

        for (var i = 0; i < 30; i++)
            terminal.Write($"line {i}\r\n");

        Assert.True(terminal.Buffer.ScrollbackCount > 20);
        Assert.Equal(0, terminal.ScrollOffset);

        Assert.True(terminal.ScrollBy(-48));

        var offset = terminal.ScrollOffset;

        Assert.True(offset > 0);

        terminal.Write("more\r\n");

        Assert.Equal(offset + 1, terminal.ScrollOffset);
        Assert.Contains(Render(root), c => c is DrawTextCommand);

        terminal.TextInput("x");

        Assert.Equal(0, terminal.ScrollOffset);
    }

    [Fact]
    public void Shift_PageUp_scrolls_back_a_page_without_sending_anything()
    {
        var (terminal, _) = Setup();
        var typed = Typed(terminal);

        for (var i = 0; i < 30; i++)
            terminal.Write($"line {i}\r\n");

        Assert.True(terminal.KeyDown(Key.PageUp, KeyModifiers.Shift));
        Assert.Equal(8, terminal.ScrollOffset);
        Assert.Empty(typed);

        Assert.True(terminal.KeyDown(Key.PageDown, KeyModifiers.Shift));
        Assert.Equal(0, terminal.ScrollOffset);
    }

    [Fact]
    public void The_scrolled_back_view_shows_older_lines()
    {
        var (terminal, root) = Setup();

        for (var i = 0; i < 30; i++)
            terminal.Write($"line {i:00}\r\n");

        terminal.ScrollLines(1000);

        var commands = Render(root).OfType<DrawTextCommand>().Select(c => c.Text).ToList();

        Assert.Contains("line 00", commands);
        Assert.DoesNotContain("line 29", commands);
    }

    [Fact]
    public void The_alternate_screen_has_no_scrollback_view()
    {
        var (terminal, _) = Setup();

        for (var i = 0; i < 30; i++)
            terminal.Write($"line {i}\r\n");

        terminal.Write($"{Esc}[?1049h");

        Assert.False(terminal.ScrollBy(-48));
        Assert.Equal(0, terminal.ScrollOffset);
    }

    [Fact]
    public void The_cursor_is_a_filled_block_with_focus_and_an_outline_without()
    {
        var (terminal, root) = Setup();

        terminal.Write("ab");

        Assert.Contains(Render(root), c => c is StrokeRectCommand { Rect: { X: 22, Width: 7 } });

        terminal.SetFocused(true);

        Assert.Contains(Render(root), c => c is FillRectCommand { Rect: { X: 22, Width: 7, Height: 17 } });
    }

    [Fact]
    public void The_cursor_blinks_with_the_animation_clock_while_focused()
    {
        var clock = new FakeClock();
        var (terminal, root) = Setup();

        root.AnimationClock = clock;
        terminal.SetFocused(true);
        terminal.TextInput("a");

        Assert.True(terminal.IsAnimating);

        bool CursorShown() => Render(root).Any(c => c is FillRectCommand { Rect: { X: 8, Width: 7, Height: 17 } });

        Assert.True(CursorShown());

        clock.Now = TimeSpan.FromMilliseconds(600);

        Assert.False(CursorShown());

        clock.Now = TimeSpan.FromMilliseconds(1100);

        Assert.True(CursorShown());

        terminal.SetFocused(false);

        Assert.False(terminal.IsAnimating);
    }

    [Fact]
    public void A_hidden_cursor_is_not_drawn_and_the_title_and_bell_are_forwarded()
    {
        var (terminal, root) = Setup();
        var titles = 0;
        var bells = 0;

        terminal.TitleChanged += (_, _) => titles++;
        terminal.Bell += (_, _) => bells++;
        terminal.SetFocused(true);
        terminal.Write($"{Esc}[?25l{Esc}]0;Shell\u0007\u0007");

        Assert.DoesNotContain(Render(root), c => c is FillRectCommand { Rect: { Width: 7, Height: 17 } });
        Assert.Equal("Shell", terminal.Title);
        Assert.Equal((1, 1), (titles, bells));
    }

    [Fact]
    public void Bytes_can_be_written_in_pieces()
    {
        var (terminal, _) = Setup();
        var bytes = System.Text.Encoding.UTF8.GetBytes("é");

        terminal.Write(bytes.AsSpan(0, 1));
        terminal.Write(bytes.AsSpan(1));

        Assert.Equal("é", terminal.Buffer.GetRowText(0));
    }
}
