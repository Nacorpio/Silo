using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class PasswordBoxTests
{
    static (Box Root, PasswordBox Box) Setup()
    {
        var box = new PasswordBox();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "padding": "10px", "align-items": "start" }, "PasswordBox": { "width": "100px" } } }""";

        root.Children.Add(box);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        return (root, box);
    }

    /// <summary>Renders the tree and returns the texts that were drawn.</summary>
    static List<string> Drawn(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text).ToList();
    }

    [Fact]
    public void The_text_is_kept_but_only_dots_are_drawn_until_it_is_revealed()
    {
        var (root, box) = Setup();

        box.TextInput("abc");

        Assert.Equal("abc", box.Text);
        Assert.Equal(["•••"], Drawn(root));

        box.IsRevealed = true;

        Assert.Equal(["abc"], Drawn(root));
    }

    [Fact]
    public void The_mask_character_can_be_changed_and_the_placeholder_is_never_masked()
    {
        var (root, box) = Setup();

        box.Placeholder = "Password";

        Assert.Equal(["Password"], Drawn(root));

        box.MaskCharacter = '*';
        box.TextInput("ab");

        Assert.Equal(["**"], Drawn(root));
    }

    [Fact]
    public void Editing_and_the_caret_work_on_the_real_text()
    {
        var (_, box) = Setup();

        box.TextInput("abc");
        box.KeyDown(Key.Left);
        box.KeyDown(Key.Backspace);

        Assert.Equal("ac", box.Text);
        Assert.Equal(1, box.CaretIndex);
    }

    sealed class FakeClipboard : IClipboard
    {
        public string Text { get; set; } = string.Empty;

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    [Fact]
    public void Copy_and_cut_do_nothing_while_the_text_is_hidden_and_work_when_it_is_revealed()
    {
        var (_, box) = Setup();
        var clipboard = new FakeClipboard { Text = "x" };

        box.Clipboard = clipboard;
        box.Text = "secret";
        box.KeyDown(Key.A, KeyModifiers.Control);
        box.KeyDown(Key.C, KeyModifiers.Control);
        box.KeyDown(Key.X, KeyModifiers.Control);

        Assert.Equal("x", clipboard.Text);
        Assert.Equal("secret", box.Text);

        box.IsRevealed = true;
        box.KeyDown(Key.C, KeyModifiers.Control);

        Assert.Equal("secret", clipboard.Text);
    }
}
