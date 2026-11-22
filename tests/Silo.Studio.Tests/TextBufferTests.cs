using Silo.Studio;
using Xunit;

public class TextBufferTests
{
    static TextBuffer Buffer(string text, int caret)
    {
        var buffer = new TextBuffer(text);

        buffer.MoveTo(caret, extend: false);

        return buffer;
    }

    [Fact]
    public void Typing_inserts_at_the_caret_and_moves_it_after_the_text()
    {
        var buffer = Buffer("ac", 1);

        buffer.Insert("b");

        Assert.Equal("abc", buffer.Text);
        Assert.Equal(2, buffer.Caret);
    }

    [Fact]
    public void Typing_replaces_the_selection()
    {
        var buffer = Buffer("hello world", 0);

        buffer.MoveTo(5, extend: true);
        buffer.Insert("bye");

        Assert.Equal("bye world", buffer.Text);
        Assert.False(buffer.HasSelection);
    }

    [Fact]
    public void Backspace_and_Delete_remove_one_character_or_the_selection()
    {
        var buffer = Buffer("abcd", 2);

        buffer.Backspace();
        buffer.Delete();

        Assert.Equal("ad", buffer.Text);
        Assert.Equal(1, buffer.Caret);

        buffer.SelectAll();
        buffer.Backspace();

        Assert.Equal(string.Empty, buffer.Text);
    }

    [Fact]
    public void Backspace_at_the_start_and_Delete_at_the_end_do_nothing()
    {
        var buffer = new TextBuffer("a");

        buffer.Backspace();
        buffer.MoveToEnd(extend: false);
        buffer.Delete();

        Assert.Equal("a", buffer.Text);
        Assert.False(buffer.CanUndo);
    }

    [Fact]
    public void Lines_are_counted_and_found_by_offset()
    {
        var buffer = new TextBuffer("one\ntwo\n\nfour");

        Assert.Equal(4, buffer.LineCount);
        Assert.Equal(1, buffer.LineOf(5));
        Assert.Equal(4, buffer.LineStart(1));
        Assert.Equal(7, buffer.LineEnd(1));
        Assert.Equal("four", buffer.GetLine(3));
        Assert.Equal(string.Empty, buffer.GetLine(2));
    }

    [Fact]
    public void Up_and_Down_keep_the_column_across_a_short_line()
    {
        var buffer = Buffer("abcdef\nab\nabcdef", 5);

        buffer.MoveDown(extend: false);

        Assert.Equal(9, buffer.Caret);

        buffer.MoveDown(extend: false);

        Assert.Equal(15, buffer.Caret);
    }

    [Fact]
    public void Up_on_the_first_line_goes_to_the_start_and_Down_on_the_last_to_the_end()
    {
        var buffer = Buffer("abc\ndef", 2);

        buffer.MoveUp(extend: false);

        Assert.Equal(0, buffer.Caret);

        buffer.MoveDown(extend: false);
        buffer.MoveDown(extend: false);

        Assert.Equal(7, buffer.Caret);
    }

    [Fact]
    public void Left_and_Right_collapse_a_selection_to_its_ends()
    {
        var buffer = Buffer("abcdef", 1);

        buffer.MoveTo(4, extend: true);
        buffer.MoveLeft(extend: false);

        Assert.Equal(1, buffer.Caret);
        Assert.False(buffer.HasSelection);

        buffer.MoveTo(4, extend: true);
        buffer.MoveRight(extend: false);

        Assert.Equal(4, buffer.Caret);
    }

    [Fact]
    public void Extending_a_move_selects_from_the_anchor()
    {
        var buffer = Buffer("abcdef", 2);

        buffer.MoveRight(extend: true);
        buffer.MoveRight(extend: true);

        Assert.Equal("cd", buffer.SelectedText);
        Assert.Equal(2, buffer.SelectionStart);
        Assert.Equal(4, buffer.SelectionEnd);
    }

    [Fact]
    public void Home_goes_to_the_first_character_that_is_not_a_space_and_then_to_the_start_of_the_line()
    {
        var buffer = Buffer("a\n    b", 7);

        buffer.MoveToLineStart(extend: false);

        Assert.Equal(6, buffer.Caret);

        buffer.MoveToLineStart(extend: false);

        Assert.Equal(2, buffer.Caret);
    }

    [Fact]
    public void End_goes_to_the_end_of_the_line()
    {
        var buffer = Buffer("ab\ncd", 0);

        buffer.MoveToLineEnd(extend: false);

        Assert.Equal(2, buffer.Caret);
    }

    [Fact]
    public void NewLine_keeps_the_indentation_of_the_line()
    {
        var buffer = Buffer("  a", 3);

        buffer.NewLine();

        Assert.Equal("  a\n  ", buffer.Text);
        Assert.Equal(6, buffer.Caret);
    }

    [Fact]
    public void NewLine_after_an_opening_brace_indents_one_step_further()
    {
        var buffer = Buffer("{", 1);

        buffer.NewLine();

        Assert.Equal("{\n  ", buffer.Text);
    }

    [Fact]
    public void Tab_inserts_a_step_and_Tab_on_a_selection_indents_its_lines()
    {
        var buffer = Buffer("a\nb\nc", 0);

        buffer.Tab();

        Assert.Equal("  a\nb\nc", buffer.Text);

        buffer.MoveTo(0, extend: false);
        buffer.MoveTo(5, extend: true);
        buffer.Tab();

        Assert.Equal("    a\n  b\nc", buffer.Text);
    }

    [Fact]
    public void Untab_takes_a_step_off_every_line_of_the_selection()
    {
        var buffer = new TextBuffer("    a\n  b\nc");

        buffer.SelectAll();
        buffer.Untab();

        Assert.Equal("  a\nb\nc", buffer.Text);
    }

    [Fact]
    public void A_selection_that_ends_at_the_start_of_a_line_does_not_indent_that_line()
    {
        var buffer = new TextBuffer("a\nb\nc");

        buffer.MoveTo(0, extend: false);
        buffer.MoveTo(4, extend: true);
        buffer.Tab();

        Assert.Equal("  a\n  b\nc", buffer.Text);
    }

    [Fact]
    public void Typing_a_run_of_characters_is_one_undo_step()
    {
        var buffer = new TextBuffer();

        buffer.Insert("a");
        buffer.Insert("b");
        buffer.Insert("c");

        Assert.True(buffer.Undo());
        Assert.Equal(string.Empty, buffer.Text);
        Assert.False(buffer.CanUndo);
    }

    [Fact]
    public void Moving_the_caret_or_a_new_line_starts_a_new_undo_step()
    {
        var buffer = new TextBuffer();

        buffer.Insert("a");
        buffer.NewLine();
        buffer.Insert("b");

        buffer.Undo();

        Assert.Equal("a\n", buffer.Text);

        buffer.Undo();

        Assert.Equal("a", buffer.Text);
    }

    [Fact]
    public void Redo_applies_an_undone_step_and_a_new_edit_clears_it()
    {
        var buffer = new TextBuffer("x");

        buffer.MoveToEnd(extend: false);
        buffer.Insert("y");
        buffer.Undo();

        Assert.True(buffer.CanRedo);

        buffer.Redo();

        Assert.Equal("xy", buffer.Text);

        buffer.Undo();
        buffer.Insert("z");

        Assert.False(buffer.CanRedo);
    }

    [Fact]
    public void Undo_restores_the_caret_and_the_selection()
    {
        var buffer = Buffer("hello", 0);

        buffer.MoveTo(5, extend: true);
        buffer.Insert("x");
        buffer.Undo();

        Assert.Equal("hello", buffer.Text);
        Assert.Equal("hello", buffer.SelectedText);
    }

    [Fact]
    public void Line_breaks_of_a_loaded_text_are_kept_when_it_is_written_back()
    {
        var buffer = new TextBuffer("a\r\nb");

        Assert.Equal("a\nb", buffer.Text);
        Assert.Equal(2, buffer.LineCount);

        buffer.MoveToEnd(extend: false);
        buffer.NewLine();

        Assert.Equal("a\r\nb\r\n", buffer.ToFileText());
    }

    [Fact]
    public void Pasted_text_with_CRLF_is_normalized()
    {
        var buffer = new TextBuffer();

        buffer.Insert("a\r\nb");

        Assert.Equal("a\nb", buffer.Text);
    }

    [Fact]
    public void Changed_is_raised_by_edits_and_moves_and_Reset_clears_the_history()
    {
        var buffer = new TextBuffer();
        var raised = 0;

        buffer.Changed += (_, _) => raised++;

        buffer.Insert("a");
        buffer.MoveTo(0, extend: false);

        Assert.Equal(2, raised);

        buffer.Reset("fresh");

        Assert.Equal("fresh", buffer.Text);
        Assert.False(buffer.CanUndo);
    }
}
