using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class TextAreaTests
{
    sealed class FakeClipboard : IClipboard
    {
        public string Text { get; set; } = string.Empty;

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    // The fixed measurer makes a character 7 pixels wide and a line 14 high. With fewer than 10 lines the line numbers take
    // 7 pixels for the digit and 16 for the space around it, so the text starts at x = 23.
    static (Box Root, TextArea Editor, PointerRouter Router) Setup(string text)
    {
        var editor = new TextArea(new TextBuffer(text)) { ShowLineNumbers = true };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "align-items": "start" }, "TextArea": { "width": "300px", "height": "140px" } } }""";

        root.Children.Add(editor);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 200));

        return (root, editor, new PointerRouter(root));
    }

    static IReadOnlyList<DrawCommand> Commands(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands;
    }

    static string Numbers(Box root)
        => string.Join(",", Commands(root).OfType<DrawTextCommand>().Where(c => c.X < 23).Select(c => c.Text));

    [Fact]
    public void Typed_text_goes_into_the_buffer_and_control_characters_are_dropped()
    {
        var (_, editor, _) = Setup(string.Empty);

        Assert.True(editor.TextInput("ab"));
        Assert.False(editor.TextInput("\n"));

        Assert.Equal("ab", editor.Buffer.Text);
    }

    [Fact]
    public void TextChanged_is_raised_for_edits_but_not_for_moves_of_the_caret()
    {
        var (_, editor, _) = Setup("abc");
        var raised = 0;

        editor.TextChanged += (_, _) => raised++;

        editor.KeyDown(Key.Right, KeyModifiers.None);
        editor.KeyDown(Key.Right, KeyModifiers.Shift);

        Assert.Equal(0, raised);

        editor.TextInput("x");
        editor.KeyDown(Key.Z, KeyModifiers.Control);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Enter_starts_an_indented_line_and_Tab_and_Shift_Tab_change_the_indentation()
    {
        var (_, editor, _) = Setup("  a");

        editor.KeyDown(Key.End, KeyModifiers.None);
        editor.KeyDown(Key.Enter, KeyModifiers.None);
        editor.KeyDown(Key.Tab, KeyModifiers.None);

        Assert.Equal("  a\n    ", editor.Buffer.Text);

        editor.KeyDown(Key.Tab, KeyModifiers.Shift);

        Assert.Equal("  a\n  ", editor.Buffer.Text);
    }

    [Fact]
    public void Shift_with_an_arrow_selects_and_Control_A_selects_everything()
    {
        var (_, editor, _) = Setup("abc\ndef");

        editor.KeyDown(Key.Right, KeyModifiers.Shift);
        editor.KeyDown(Key.Right, KeyModifiers.Shift);

        Assert.Equal("ab", editor.Buffer.SelectedText);

        editor.KeyDown(Key.A, KeyModifiers.Control);

        Assert.Equal("abc\ndef", editor.Buffer.SelectedText);
    }

    [Fact]
    public void Control_Z_undoes_and_Control_Y_and_Control_Shift_Z_redo()
    {
        var (_, editor, _) = Setup("a");

        editor.Buffer.MoveToEnd(extend: false);
        editor.TextInput("b");
        editor.KeyDown(Key.Z, KeyModifiers.Control);

        Assert.Equal("a", editor.Buffer.Text);

        editor.KeyDown(Key.Y, KeyModifiers.Control);

        Assert.Equal("ab", editor.Buffer.Text);

        editor.KeyDown(Key.Z, KeyModifiers.Control);
        editor.KeyDown(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Equal("ab", editor.Buffer.Text);
    }

    [Fact]
    public void Copy_cut_and_paste_use_the_clipboard_and_do_nothing_without_one()
    {
        var (_, editor, _) = Setup("hello world");
        var clipboard = new FakeClipboard();

        editor.KeyDown(Key.A, KeyModifiers.Control);
        editor.KeyDown(Key.C, KeyModifiers.Control);

        Assert.Equal("hello world", editor.Buffer.Text);

        editor.Clipboard = clipboard;

        editor.KeyDown(Key.C, KeyModifiers.Control);

        Assert.Equal("hello world", clipboard.Text);

        editor.KeyDown(Key.X, KeyModifiers.Control);

        Assert.Equal(string.Empty, editor.Buffer.Text);

        editor.KeyDown(Key.V, KeyModifiers.Control);
        editor.KeyDown(Key.V, KeyModifiers.Control);

        Assert.Equal("hello worldhello world", editor.Buffer.Text);
    }

    [Fact]
    public void A_key_that_the_editor_does_not_handle_is_not_consumed()
    {
        var (_, editor, _) = Setup("a");

        Assert.False(editor.KeyDown(Key.F5, KeyModifiers.None));
        Assert.False(editor.KeyDown(Key.C, KeyModifiers.None));
    }

    [Fact]
    public void The_lines_and_their_numbers_are_drawn()
    {
        var (root, _, _) = Setup("one\ntwo\nthree");

        var texts = Commands(root).OfType<DrawTextCommand>().ToList();

        Assert.Equal("1,2,3", Numbers(root));
        Assert.Equal(23, texts.Single(c => c.Text == "two").X);
        Assert.Equal(14, texts.Single(c => c.Text == "two").Y);
    }

    [Fact]
    public void The_caret_is_drawn_only_while_the_editor_has_the_focus()
    {
        var (root, editor, _) = Setup("abc");

        editor.Buffer.MoveTo(2, extend: false);

        Assert.DoesNotContain(Commands(root), c => c is FillRectCommand { Rect.Width: 1 });

        new FocusManager().Focus(editor);

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": {} }""", "t.jsonc")]));

        Assert.Contains(Commands(root), c => c is FillRectCommand { Rect: { Width: 1, X: 37 } });
    }

    [Fact]
    public void The_selection_is_filled_behind_the_text()
    {
        var (root, editor, _) = Setup("abcdef");

        editor.Buffer.MoveTo(1, extend: false);
        editor.Buffer.MoveTo(4, extend: true);

        var fill = Commands(root).OfType<FillRectCommand>().Single(c => c.Color == new Color(59, 130, 246, 90));

        Assert.Equal(23 + 7, fill.Rect.X);
        Assert.Equal(21, fill.Rect.Width);
    }

    [Fact]
    public void Pressing_the_pointer_in_the_text_puts_the_caret_at_the_nearest_boundary()
    {
        var (root, editor, router) = Setup("abc\ndefg");

        Commands(root);

        router.Down(23 + (7 * 2) + 2, 14 + 3);
        router.Up(23 + (7 * 2) + 2, 14 + 3);

        Assert.Equal(6, editor.Buffer.Caret);
        Assert.False(editor.Buffer.HasSelection);
    }

    [Fact]
    public void Dragging_with_the_pointer_held_extends_the_selection_and_it_stops_at_release()
    {
        var (root, editor, router) = Setup("abc\ndefg");

        Commands(root);

        router.Down(23 + 7, 3);
        router.Move(23 + (7 * 2), 14 + 3);

        Assert.Equal("bc\nde", editor.Buffer.SelectedText);

        router.Up(23 + (7 * 2), 14 + 3);
        router.Move(23, 3);

        Assert.Equal("bc\nde", editor.Buffer.SelectedText);
    }

    [Fact]
    public void The_wheel_scrolls_the_lines_and_stays_within_the_text()
    {
        var text = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"line {i}"));
        var (root, editor, router) = Setup(text);

        Commands(root);

        Assert.True(router.Scroll(10, 10, 28));
        Assert.Contains("line 3", Commands(root).OfType<DrawTextCommand>().Where(c => c.Y < 14).Select(c => c.Text));

        editor.ScrollBy(100000);

        Assert.Contains("line 30", Commands(root).OfType<DrawTextCommand>().Select(c => c.Text));
        Assert.False(editor.ScrollBy(1));
    }

    [Fact]
    public void Moving_the_caret_below_the_last_visible_line_scrolls_it_into_view()
    {
        var text = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"line {i}"));
        var (root, editor, _) = Setup(text);

        Commands(root);

        editor.KeyDown(Key.End, KeyModifiers.Control);

        Assert.Contains("line 30", Commands(root).OfType<DrawTextCommand>().Select(c => c.Text));
        Assert.DoesNotContain("line 1", Commands(root).OfType<DrawTextCommand>().Select(c => c.Text));
    }

    [Fact]
    public void Page_Down_moves_the_caret_by_a_screenful()
    {
        var text = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"line {i}"));
        var (root, editor, _) = Setup(text);

        Commands(root);

        editor.KeyDown(Key.PageDown, KeyModifiers.None);

        Assert.Equal(9, editor.Buffer.Line);

        editor.KeyDown(Key.PageUp, KeyModifiers.None);

        Assert.Equal(0, editor.Buffer.Line);
    }

    [Fact]
    public void Line_numbers_are_off_by_default_and_the_text_then_starts_at_the_left_edge()
    {
        var (root, editor, _) = Setup("one\ntwo");
        var builder = new DisplayListBuilder();

        editor.ShowLineNumbers = false;
        root.Render(builder);

        var texts = builder.Build().Commands.OfType<DrawTextCommand>().ToList();

        Assert.Equal(["one", "two"], texts.Select(t => t.Text));
        Assert.Equal(0, texts[0].X);
        Assert.False(new TextArea().ShowLineNumbers);
    }
}
