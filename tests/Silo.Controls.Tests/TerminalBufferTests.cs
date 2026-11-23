using System.Text;
using Silo;
using Xunit;

public class TerminalBufferTests
{
    static void Type(TerminalBuffer buffer, string text)
    {
        foreach (var character in text)
            buffer.Write(new Rune(character));
    }

    [Fact]
    public void Written_text_goes_in_cells_and_moves_the_cursor()
    {
        var buffer = new TerminalBuffer(10, 3);

        Type(buffer, "Hi");

        Assert.Equal('H', buffer.GetCell(0, 0).Character.Value);
        Assert.Equal('i', buffer.GetCell(1, 0).Character.Value);
        Assert.Equal(2, buffer.CursorColumn);
        Assert.Equal("Hi", buffer.GetRowText(0));
    }

    [Fact]
    public void Text_takes_the_pen()
    {
        var buffer = new TerminalBuffer(10, 3) { Foreground = TerminalColor.FromIndex(1), Attributes = TerminalAttributes.Bold };

        Type(buffer, "x");

        var cell = buffer.GetCell(0, 0);

        Assert.Equal(TerminalColor.FromIndex(1), cell.Foreground);
        Assert.Equal(TerminalAttributes.Bold, cell.Attributes);
    }

    [Fact]
    public void The_wrap_waits_until_the_next_character()
    {
        var buffer = new TerminalBuffer(4, 3);

        Type(buffer, "abcd");

        Assert.Equal(0, buffer.CursorRow);
        Assert.Equal(3, buffer.CursorColumn);

        Type(buffer, "e");

        Assert.Equal(1, buffer.CursorRow);
        Assert.Equal("abcd", buffer.GetRowText(0));
        Assert.Equal("e", buffer.GetRowText(1));
    }

    [Fact]
    public void Without_auto_wrap_the_last_cell_is_overwritten()
    {
        var buffer = new TerminalBuffer(3, 2) { AutoWrap = false };

        Type(buffer, "abcde");

        Assert.Equal("abe", buffer.GetRowText(0));
        Assert.Equal(0, buffer.CursorRow);
    }

    [Fact]
    public void A_line_feed_on_the_last_row_scrolls_and_keeps_the_top_line_in_the_scrollback()
    {
        var buffer = new TerminalBuffer(5, 2);

        Type(buffer, "one");
        buffer.CarriageReturn();
        buffer.LineFeed();
        Type(buffer, "two");
        buffer.CarriageReturn();
        buffer.LineFeed();
        Type(buffer, "three");

        Assert.Equal(1, buffer.ScrollbackCount);
        Assert.Equal("one", buffer.GetScrollbackText(0));
        Assert.Equal("two", buffer.GetRowText(0));
        Assert.Equal("three", buffer.GetRowText(1));
    }

    [Fact]
    public void The_scrollback_drops_the_oldest_lines_above_its_limit()
    {
        var buffer = new TerminalBuffer(5, 1) { ScrollbackLimit = 2 };

        for (var i = 0; i < 5; i++)
        {
            Type(buffer, i.ToString());
            buffer.CarriageReturn();
            buffer.LineFeed();
        }

        Assert.Equal(2, buffer.ScrollbackCount);
        Assert.Equal("3", buffer.GetScrollbackText(0));
        Assert.Equal("4", buffer.GetScrollbackText(1));
    }

    [Fact]
    public void A_scroll_region_scrolls_inside_itself_and_feeds_no_scrollback()
    {
        var buffer = new TerminalBuffer(4, 4);

        for (var row = 0; row < 4; row++)
        {
            buffer.MoveTo(row, 0);
            Type(buffer, row.ToString());
        }

        buffer.SetScrollRegion(1, 2);
        buffer.MoveTo(2, 0);
        buffer.LineFeed();

        Assert.Equal("0", buffer.GetRowText(0));
        Assert.Equal("2", buffer.GetRowText(1));
        Assert.Equal("", buffer.GetRowText(2));
        Assert.Equal("3", buffer.GetRowText(3));
        Assert.Equal(0, buffer.ScrollbackCount);
    }

    [Fact]
    public void A_reverse_index_on_the_top_row_scrolls_down()
    {
        var buffer = new TerminalBuffer(4, 3);

        Type(buffer, "a");
        buffer.MoveTo(0, 0);
        buffer.ReverseIndex();

        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal("a", buffer.GetRowText(1));
    }

    [Fact]
    public void Erasing_fills_with_the_pen_background()
    {
        var buffer = new TerminalBuffer(4, 2);

        Type(buffer, "abcd");
        buffer.Background = TerminalColor.FromIndex(4);
        buffer.MoveTo(0, 1);
        buffer.EraseInLine(0);

        Assert.Equal("a", buffer.GetRowText(0));
        Assert.Equal(TerminalColor.FromIndex(4), buffer.GetCell(3, 0).Background);
        Assert.Equal(default, buffer.GetCell(0, 0).Background);
    }

    [Fact]
    public void Erase_in_display_has_four_modes()
    {
        var buffer = new TerminalBuffer(3, 3);

        void Fill()
        {
            for (var row = 0; row < 3; row++)
            {
                buffer.MoveTo(row, 0);
                Type(buffer, "xxx");
            }

            buffer.MoveTo(1, 1);
        }

        Fill();
        buffer.EraseInDisplay(0);

        Assert.Equal(["xxx", "x", ""], new[] { buffer.GetRowText(0), buffer.GetRowText(1), buffer.GetRowText(2) });

        Fill();
        buffer.EraseInDisplay(1);

        Assert.Equal(["", "  x", "xxx"], new[] { buffer.GetRowText(0), buffer.GetRowText(1), buffer.GetRowText(2) });

        Fill();
        buffer.EraseInDisplay(2);

        Assert.All(Enumerable.Range(0, 3), row => Assert.Equal("", buffer.GetRowText(row)));
    }

    [Fact]
    public void Insert_and_delete_lines_work_inside_the_scroll_region_and_lose_no_scrollback()
    {
        var buffer = new TerminalBuffer(3, 4);

        for (var row = 0; row < 4; row++)
        {
            buffer.MoveTo(row, 0);
            Type(buffer, row.ToString());
        }

        buffer.MoveTo(1, 0);
        buffer.InsertLines(1);

        Assert.Equal(["0", "", "1", "2"], Enumerable.Range(0, 4).Select(buffer.GetRowText));

        buffer.MoveTo(0, 0);
        buffer.DeleteLines(1);

        Assert.Equal(["", "1", "2", ""], Enumerable.Range(0, 4).Select(buffer.GetRowText));
        Assert.Equal(0, buffer.ScrollbackCount);
    }

    [Fact]
    public void Insert_and_delete_characters_shift_the_rest_of_the_line()
    {
        var buffer = new TerminalBuffer(6, 1);

        Type(buffer, "abcdef");
        buffer.MoveTo(0, 1);
        buffer.InsertCharacters(2);

        Assert.Equal("a  bcd", buffer.GetRowText(0));

        buffer.DeleteCharacters(2);

        Assert.Equal("abcd", buffer.GetRowText(0));
    }

    [Fact]
    public void Tab_goes_to_the_next_multiple_of_eight()
    {
        var buffer = new TerminalBuffer(20, 1);

        buffer.Tab();

        Assert.Equal(8, buffer.CursorColumn);

        buffer.MoveTo(0, 9);
        buffer.Tab();

        Assert.Equal(16, buffer.CursorColumn);

        buffer.Tab();

        Assert.Equal(19, buffer.CursorColumn);
    }

    [Fact]
    public void The_cursor_is_clamped_to_the_screen()
    {
        var buffer = new TerminalBuffer(5, 3);

        buffer.MoveTo(99, 99);

        Assert.Equal((4, 2), (buffer.CursorColumn, buffer.CursorRow));

        buffer.CursorUp(99);
        buffer.CursorBack(99);

        Assert.Equal((0, 0), (buffer.CursorColumn, buffer.CursorRow));
    }

    [Fact]
    public void Save_and_restore_bring_back_the_position_and_the_pen()
    {
        var buffer = new TerminalBuffer(5, 3);

        buffer.MoveTo(1, 2);
        buffer.Foreground = TerminalColor.FromIndex(2);
        buffer.SaveCursor();
        buffer.MoveTo(0, 0);
        buffer.ResetPen();
        buffer.RestoreCursor();

        Assert.Equal((2, 1), (buffer.CursorColumn, buffer.CursorRow));
        Assert.Equal(TerminalColor.FromIndex(2), buffer.Foreground);
    }

    [Fact]
    public void The_alternate_screen_starts_blank_has_no_scrollback_and_gives_the_main_screen_back()
    {
        var buffer = new TerminalBuffer(4, 2);

        Type(buffer, "main");
        buffer.UseAlternateScreen(true);

        Assert.True(buffer.IsAlternateScreen);
        Assert.Equal("", buffer.GetRowText(0));

        Type(buffer, "alt");
        buffer.CarriageReturn();
        buffer.LineFeed();
        buffer.LineFeed();

        Assert.Equal(0, buffer.ScrollbackCount);

        buffer.UseAlternateScreen(false);

        Assert.Equal("main", buffer.GetRowText(0));
    }

    [Fact]
    public void Shrinking_keeps_the_cursor_line_and_pushes_the_top_lines_to_the_scrollback()
    {
        var buffer = new TerminalBuffer(5, 4);

        for (var row = 0; row < 4; row++)
        {
            buffer.MoveTo(row, 0);
            Type(buffer, row.ToString());
        }

        buffer.Resize(5, 2);

        Assert.Equal(2, buffer.ScrollbackCount);
        Assert.Equal("2", buffer.GetRowText(0));
        Assert.Equal("3", buffer.GetRowText(1));
        Assert.Equal(1, buffer.CursorRow);
    }

    [Fact]
    public void Growing_and_changing_the_width_pads_and_cuts_rows()
    {
        var buffer = new TerminalBuffer(4, 2);

        Type(buffer, "abcd");
        buffer.Resize(6, 3);

        Assert.Equal((6, 3), (buffer.Columns, buffer.Rows));
        Assert.Equal("abcd", buffer.GetRowText(0));

        buffer.Resize(2, 3);

        Assert.Equal("ab", buffer.GetRowText(0));
        Assert.True(buffer.CursorColumn < 2);
    }

    [Fact]
    public void Changing_the_title_raises_TitleChanged_once_per_change()
    {
        var buffer = new TerminalBuffer(2, 2);
        var raised = 0;

        buffer.TitleChanged += (_, _) => raised++;
        buffer.Title = "a";
        buffer.Title = "a";
        buffer.Title = "b";

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var buffer = new TerminalBuffer(3, 2);

        Type(buffer, "abc");
        buffer.Foreground = TerminalColor.FromIndex(1);
        buffer.CursorVisible = false;
        buffer.UseAlternateScreen(true);
        buffer.Reset();

        Assert.False(buffer.IsAlternateScreen);
        Assert.True(buffer.CursorVisible);
        Assert.Equal("", buffer.GetRowText(0));
        Assert.Equal(default, buffer.Foreground);
    }
}
