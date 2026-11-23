using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class TextBoxTests
{
    static (Box Root, TextBox Box, FocusManager Focus, PointerRouter Router) Setup()
    {
        var box = new TextBox();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "padding": "10px", "align-items": "start" }, "TextBox": { "width": "100px" } } }""";

        root.Children.Add(box);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        return (root, box, new FocusManager(), new PointerRouter(root));
    }

    [Fact]
    public void Typed_text_goes_in_at_the_caret_and_control_characters_are_dropped()
    {
        var (_, box, _, _) = Setup();

        Assert.True(box.TextInput("ab"));
        Assert.True(box.TextInput("\tc\n"));
        Assert.False(box.TextInput("\r"));

        Assert.Equal("abc", box.Text);
        Assert.Equal(3, box.CaretIndex);

        box.KeyDown(Key.Left);
        box.TextInput("X");

        Assert.Equal("abXc", box.Text);
        Assert.Equal(3, box.CaretIndex);
    }

    [Fact]
    public void Backspace_and_delete_remove_the_character_before_and_after_the_caret()
    {
        var (_, box, _, _) = Setup();
        var changes = 0;

        box.Text = "abcd";
        box.TextChanged += (_, _) => changes++;
        box.KeyDown(Key.Home);

        Assert.False(box.KeyDown(Key.Backspace));
        Assert.True(box.KeyDown(Key.Delete));
        Assert.Equal("bcd", box.Text);

        box.KeyDown(Key.End);

        Assert.True(box.KeyDown(Key.Backspace));
        Assert.Equal("bc", box.Text);
        Assert.False(box.KeyDown(Key.Delete));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void The_arrows_home_and_end_move_the_caret_within_the_text()
    {
        var (_, box, _, _) = Setup();

        box.Text = "abc";

        Assert.False(box.KeyDown(Key.Right));
        Assert.True(box.KeyDown(Key.Left));
        Assert.Equal(2, box.CaretIndex);

        Assert.True(box.KeyDown(Key.Home));
        Assert.False(box.KeyDown(Key.Left));
        Assert.Equal(0, box.CaretIndex);

        Assert.True(box.KeyDown(Key.End));
        Assert.Equal(3, box.CaretIndex);
    }

    [Fact]
    public void Enter_raises_Submitted_without_changing_the_text()
    {
        var (_, box, _, _) = Setup();
        var submitted = 0;

        box.Text = "hi";
        box.Submitted += (_, _) => submitted++;

        Assert.False(box.KeyDown(Key.Enter));
        Assert.Equal(1, submitted);
        Assert.Equal("hi", box.Text);
    }

    [Fact]
    public void Pressing_inside_the_text_puts_the_caret_at_the_nearest_boundary()
    {
        var (_, box, focus, router) = Setup();

        box.Text = "abcd";

        router.Down(10 + 7 * 2 + 2, 15);
        focus.FocusFrom(router.Hovered);

        Assert.Equal(2, box.CaretIndex);
        Assert.Same(box, focus.Focused);
        Assert.Contains("focus", box.PseudoStates);
    }

    [Fact]
    public void Pressing_elsewhere_takes_the_focus_away_and_keys_only_reach_the_focused_box()
    {
        var (_, box, focus, _) = Setup();
        var label = new TextBlock();

        box.Parent!.Children.Add(label);

        Assert.False(focus.FocusFrom(label));
        Assert.Null(focus.Focused);
        Assert.False(focus.TextInput("x"));
        Assert.Equal(string.Empty, box.Text);

        Assert.True(focus.FocusFrom(box));
        Assert.True(focus.TextInput("x"));
        Assert.Equal("x", box.Text);

        Assert.True(focus.FocusFrom(label));
        Assert.Null(focus.Focused);
        Assert.False(focus.TextInput("y"));
        Assert.True(focus.FocusFrom(box));

        Assert.True(focus.FocusFrom(null));
        Assert.DoesNotContain("focus", box.PseudoStates);
    }

    [Fact]
    public void The_caret_is_drawn_only_while_focused_and_the_placeholder_only_while_empty()
    {
        var (root, box, focus, _) = Setup();
        var caret = new Color(1, 2, 3);

        box.Placeholder = "Name";
        box.SetValue(TextBox.CaretColorProperty, caret);

        Assert.DoesNotContain(Commands(root), c => c is FillRectCommand f && f.Color == caret);
        Assert.Contains(Commands(root), c => c is DrawTextCommand { Text: "Name" });

        focus.FocusFrom(box);
        box.TextInput("ab");

        Assert.Contains(Commands(root), c => c is FillRectCommand f && f.Color == caret && f.Rect.X == 14);
        Assert.DoesNotContain(Commands(root), c => c is DrawTextCommand { Text: "Name" });
    }

    [Fact]
    public void Long_text_scrolls_so_that_the_caret_stays_inside_the_box()
    {
        var (root, box, _, _) = Setup();

        box.Text = new string('a', 30);

        var draw = Commands(root).OfType<DrawTextCommand>().Single(c => c.Text.Length == 30);

        Assert.True(draw.X < 10);
    }

    /// <summary>Renders the tree and returns the commands.</summary>
    static IReadOnlyList<DrawCommand> Commands(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands;
    }

    [Fact]
    public void The_ancestors_of_the_focused_Control_have_the_focus_within_pseudo_state()
    {
        var (root, box, focus, _) = Setup();

        Assert.True(focus.Focus(box));
        Assert.Contains("focus-within", root.PseudoStates);
        Assert.Contains("focus", box.PseudoStates);
        Assert.DoesNotContain("focus-within", box.PseudoStates);

        Assert.True(focus.Focus(null));
        Assert.DoesNotContain("focus-within", root.PseudoStates);
    }
}
