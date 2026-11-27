using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class TextAreaWrapTests
{
    // The fixed measurer makes a character 7 pixels wide, so a 150 pixel area without line numbers holds 21 characters.
    static (Box Root, TextArea Editor) Setup(string text, bool wrap = true, bool numbers = false)
    {
        var editor = new TextArea(new TextBuffer(text)) { WordWrap = wrap, ShowLineNumbers = numbers };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "align-items": "start" }, "TextArea": { "width": "150px", "height": "140px" } } }""";

        root.Children.Add(editor);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 200));

        var builder = new DisplayListBuilder();

        root.Render(builder);

        return (root, editor);
    }

    [Fact]
    public void Wrap_is_off_by_default()
    {
        Assert.False(new TextArea().WordWrap);
    }

    [Fact]
    public void A_long_line_breaks_after_the_last_space_that_fits()
    {
        var (_, editor) = Setup("aaaa bbbb cccc dddd eeee ffff");

        Assert.Equal(2, editor.VisualLineCount);
    }

    [Fact]
    public void A_word_wider_than_the_area_breaks_inside_the_word()
    {
        var (_, editor) = Setup(new string('x', 50));

        Assert.Equal(3, editor.VisualLineCount);
    }

    [Fact]
    public void Without_wrap_every_line_is_one_row()
    {
        var (_, editor) = Setup(new string('x', 50) + "\nb", wrap: false);

        Assert.Equal(2, editor.VisualLineCount);
    }

    [Fact]
    public void Down_moves_to_the_next_visual_row_of_the_same_line()
    {
        var (_, editor) = Setup("aaaa bbbb cccc dddd eeee ffff");

        editor.Buffer.MoveTo(2, extend: false);
        editor.KeyDown(Key.Down, KeyModifiers.None);

        Assert.Equal(0, editor.Buffer.Line);
        Assert.True(editor.Buffer.Column > 20);
    }

    [Fact]
    public void Up_returns_to_the_column_the_caret_started_in()
    {
        var (_, editor) = Setup("aaaa bbbb cccc dddd eeee ffff");

        editor.Buffer.MoveTo(2, extend: false);
        editor.KeyDown(Key.Down, KeyModifiers.None);
        editor.KeyDown(Key.Up, KeyModifiers.None);

        Assert.Equal(2, editor.Buffer.Column);
    }

    [Fact]
    public void Home_and_End_stay_inside_the_visual_row()
    {
        var (_, editor) = Setup("aaaa bbbb cccc dddd eeee ffff");

        editor.Buffer.MoveTo(25, extend: false);
        editor.KeyDown(Key.Home, KeyModifiers.None);

        Assert.Equal(20, editor.Buffer.Column);

        editor.KeyDown(Key.End, KeyModifiers.None);

        Assert.Equal(29, editor.Buffer.Column);
    }

    [Fact]
    public void Line_numbers_are_drawn_once_per_logical_line()
    {
        var (root, _) = Setup(new string('x', 50) + "\nb", numbers: true);
        var builder = new DisplayListBuilder();

        root.Render(builder);

        var numbers = builder.Build().Commands.OfType<DrawTextCommand>().Where(c => c.Text is "1" or "2").ToList();

        Assert.Equal(2, numbers.Count);
    }
}
