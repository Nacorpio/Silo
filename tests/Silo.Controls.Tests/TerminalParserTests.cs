using System.Text;
using Silo;
using Xunit;

public class TerminalParserTests
{
    const string Esc = "\u001b";

    static (TerminalBuffer Buffer, TerminalParser Parser) Setup(int columns = 20, int rows = 5)
    {
        var buffer = new TerminalBuffer(columns, rows);

        return (buffer, new TerminalParser(buffer));
    }

    [Fact]
    public void Plain_text_and_line_endings_are_written()
    {
        var (buffer, parser) = Setup();

        parser.Feed("hello\r\nworld");

        Assert.Equal("hello", buffer.GetRowText(0));
        Assert.Equal("world", buffer.GetRowText(1));
    }

    [Fact]
    public void A_bare_line_feed_keeps_the_column()
    {
        var (buffer, parser) = Setup();

        parser.Feed("ab\ncd");

        Assert.Equal("ab", buffer.GetRowText(0));
        Assert.Equal("  cd", buffer.GetRowText(1));
    }

    [Fact]
    public void Backspace_and_tab_move_the_cursor()
    {
        var (buffer, parser) = Setup();

        parser.Feed("abc\b\bX\tY");

        Assert.Equal("aXc     Y", buffer.GetRowText(0));
    }

    [Fact]
    public void The_bell_raises_an_event()
    {
        var (_, parser) = Setup();
        var rings = 0;

        parser.Bell += (_, _) => rings++;
        parser.Feed("a\u0007b\u0007");

        Assert.Equal(2, rings);
    }

    [Fact]
    public void Cursor_sequences_move_the_cursor()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[3;5H");

        Assert.Equal((4, 2), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}[A{Esc}[2D");

        Assert.Equal((2, 1), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}[H");

        Assert.Equal((0, 0), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}[2B{Esc}[7G");

        Assert.Equal((6, 2), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}[4d");

        Assert.Equal(3, buffer.CursorRow);
    }

    [Fact]
    public void Erase_sequences_clear_the_screen_and_lines()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"abc\r\ndef{Esc}[2J");

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("", buffer.GetRowText(1));

        parser.Feed($"{Esc}[1;1Hxyz{Esc}[1;2H{Esc}[K");

        Assert.Equal("x", buffer.GetRowText(0));
    }

    [Fact]
    public void SGR_sets_16_colors_and_styles_and_resets_them()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[1;31;44mA{Esc}[0mB");

        var a = buffer.GetCell(0, 0);
        var b = buffer.GetCell(1, 0);

        Assert.Equal(TerminalColor.FromIndex(1), a.Foreground);
        Assert.Equal(TerminalColor.FromIndex(4), a.Background);
        Assert.Equal(TerminalAttributes.Bold, a.Attributes);
        Assert.Equal(default, b.Foreground);
        Assert.Equal(TerminalAttributes.None, b.Attributes);
    }

    [Fact]
    public void SGR_with_no_parameters_resets()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[7m{Esc}[mA");

        Assert.Equal(TerminalAttributes.None, buffer.GetCell(0, 0).Attributes);
    }

    [Fact]
    public void SGR_bright_colors_use_the_upper_palette_half()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[92;103mA");

        Assert.Equal(TerminalColor.FromIndex(10), buffer.GetCell(0, 0).Foreground);
        Assert.Equal(TerminalColor.FromIndex(11), buffer.GetCell(0, 0).Background);
    }

    [Fact]
    public void SGR_256_and_true_colors_are_read()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[38;5;208;48;2;10;20;30mA");

        Assert.Equal(TerminalColor.FromIndex(208), buffer.GetCell(0, 0).Foreground);
        Assert.Equal(TerminalColor.FromRgb(10, 20, 30), buffer.GetCell(0, 0).Background);
    }

    [Fact]
    public void SGR_attributes_can_be_turned_off_one_by_one()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[1;3;4;9m{Esc}[22;24mA");

        Assert.Equal(TerminalAttributes.Italic | TerminalAttributes.Strikethrough, buffer.GetCell(0, 0).Attributes);
    }

    [Fact]
    public void Default_color_codes_put_the_default_back()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[31;42m{Esc}[39;49mA");

        Assert.Equal(default, buffer.GetCell(0, 0).Foreground);
        Assert.Equal(default, buffer.GetCell(0, 0).Background);
    }

    [Fact]
    public void Insert_delete_and_erase_characters_and_lines_are_applied()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"abcdef{Esc}[1;3H{Esc}[2@");

        Assert.Equal("ab  cdef", buffer.GetRowText(0));

        parser.Feed($"{Esc}[2P");

        Assert.Equal("abcdef", buffer.GetRowText(0));

        parser.Feed($"{Esc}[3X");

        Assert.Equal("ab   f", buffer.GetRowText(0));

        parser.Feed($"{Esc}[L");

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("ab   f", buffer.GetRowText(1));
    }

    [Fact]
    public void A_scroll_region_and_scroll_commands_work()
    {
        var (buffer, parser) = Setup(5, 4);

        parser.Feed($"0\r\n1\r\n2\r\n3{Esc}[2;3r");

        Assert.Equal((1, 2), (buffer.ScrollTop, buffer.ScrollBottom));
        Assert.Equal((0, 0), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}[S");

        Assert.Equal(["0", "2", "", "3"], Enumerable.Range(0, 4).Select(buffer.GetRowText));

        parser.Feed($"{Esc}[T");

        Assert.Equal(["0", "", "2", "3"], Enumerable.Range(0, 4).Select(buffer.GetRowText));
    }

    [Fact]
    public void Save_and_restore_cursor_work_in_both_spellings()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[2;3H{Esc}7{Esc}[1;1H{Esc}8");

        Assert.Equal((2, 1), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}[4;4H{Esc}[s{Esc}[1;1H{Esc}[u");

        Assert.Equal((3, 3), (buffer.CursorColumn, buffer.CursorRow));
    }

    [Fact]
    public void Index_next_line_and_reverse_index_move_vertically()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"ab{Esc}Dc");

        Assert.Equal((3, 1), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}Ex");

        Assert.Equal((1, 2), (buffer.CursorColumn, buffer.CursorRow));

        parser.Feed($"{Esc}M");

        Assert.Equal(1, buffer.CursorRow);
    }

    [Fact]
    public void Private_modes_toggle_the_cursor_wrap_keys_and_paste()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[?25l{Esc}[?7l{Esc}[?1h{Esc}[?2004h");

        Assert.False(buffer.CursorVisible);
        Assert.False(buffer.AutoWrap);
        Assert.True(buffer.ApplicationCursorKeys);
        Assert.True(buffer.BracketedPaste);

        parser.Feed($"{Esc}[?25h{Esc}[?7h{Esc}[?1l{Esc}[?2004l");

        Assert.True(buffer.CursorVisible);
        Assert.True(buffer.AutoWrap);
        Assert.False(buffer.ApplicationCursorKeys);
        Assert.False(buffer.BracketedPaste);
    }

    [Fact]
    public void The_alternate_screen_1049_saves_and_restores_the_main_screen_and_cursor()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"main{Esc}[?1049hfull screen{Esc}[?1049l");

        Assert.False(buffer.IsAlternateScreen);
        Assert.Equal("main", buffer.GetRowText(0));
        Assert.Equal(4, buffer.CursorColumn);
    }

    [Fact]
    public void The_title_is_set_by_OSC_0_and_2_ended_by_BEL_or_ST()
    {
        var (buffer, parser) = Setup();
        var rings = 0;

        parser.Bell += (_, _) => rings++;
        parser.Feed($"{Esc}]0;First\u0007");

        Assert.Equal("First", buffer.Title);

        parser.Feed($"{Esc}]2;Second{Esc}\\x");

        Assert.Equal("Second", buffer.Title);
        Assert.Equal("x", buffer.GetRowText(0));
        Assert.Equal(0, rings);
    }

    [Fact]
    public void Other_operating_system_commands_and_unknown_sequences_print_nothing()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}]8;;http://x{Esc}\\link{Esc}]8;;{Esc}\\");
        parser.Feed($"{Esc}[?9999z{Esc}[>5q{Esc}P1$rxx{Esc}\\{Esc}=ok");

        Assert.Equal("linkok", buffer.GetRowText(0));
    }

    [Fact]
    public void Character_set_designation_is_consumed_and_line_drawing_maps_characters()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}(0lqk{Esc}(Blqk");

        Assert.Equal("┌─┐lqk", buffer.GetRowText(0));
    }

    [Fact]
    public void Status_reports_are_sent_to_Reply()
    {
        var (_, parser) = Setup();
        var replies = new List<string>();

        parser.Reply = replies.Add;
        parser.Feed($"{Esc}[3;4H{Esc}[6n{Esc}[5n{Esc}[c{Esc}[>c");

        Assert.Equal([$"{Esc}[3;4R", $"{Esc}[0n", $"{Esc}[?1;2c", $"{Esc}[>0;0;0c"], replies);
    }

    [Fact]
    public void A_sequence_cut_between_two_feeds_is_finished_by_the_next_one()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[3");
        parser.Feed("1mR");

        Assert.Equal(TerminalColor.FromIndex(1), buffer.GetCell(0, 0).Foreground);
    }

    [Fact]
    public void UTF8_bytes_can_be_cut_in_the_middle_of_a_character()
    {
        var (buffer, parser) = Setup();
        var bytes = Encoding.UTF8.GetBytes("é€😀");

        parser.Feed(bytes.AsSpan(0, 1));
        parser.Feed(bytes.AsSpan(1, 3));
        parser.Feed(bytes.AsSpan(4, 3));
        parser.Feed(bytes.AsSpan(7));

        Assert.Equal("é€😀", buffer.GetRowText(0));
    }

    [Fact]
    public void CAN_and_SUB_abort_a_sequence()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}[31\u0018A{Esc}[1\u001aB");

        Assert.Equal("AB", buffer.GetRowText(0));
        Assert.Equal(default, buffer.GetCell(0, 0).Foreground);
    }

    [Fact]
    public void A_full_reset_clears_the_screen_and_the_line_drawing_set()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"abc{Esc}(0{Esc}cq");

        Assert.Equal("q", buffer.GetRowText(0));
    }

    [Fact]
    public void Long_unterminated_strings_do_not_grow_without_limit_and_the_parser_recovers()
    {
        var (buffer, parser) = Setup();

        parser.Feed($"{Esc}]0;{new string('x', 100000)}");
        parser.Feed($"\u0007ok");

        Assert.Equal(4094, buffer.Title.Length);
        Assert.Equal("ok", buffer.GetRowText(0));
    }
}
