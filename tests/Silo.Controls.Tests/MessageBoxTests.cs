using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class MessageBoxTests
{
    const string Sheet = """
        {
          "rules": {
            "#root": { "width": "400px", "height": "300px" },
            "Dialog": { "flex-direction": "column", "width": "300px" },
            "DialogButtons": { "flex-direction": "row", "gap": "4px" },
            ".messagebox-content": { "flex-direction": "row" }
          }
        }
        """;

    static (Box Root, OverlayLayer Layer, PointerRouter Router) Setup()
    {
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        root.Overlays = layer;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        return (root, layer, new PointerRouter(root, layer));
    }

    static (double X, double Y) FindButton(OverlayLayer layer, string text)
    {
        for (var y = 0; y < 300; y += 2)
            for (var x = 0; x < 400; x += 2)
                if (layer.HitTest(x, y) is Button button && button.Text == text)
                    return (x, y);

        throw new InvalidOperationException("No button " + text);
    }

    [Theory]
    [InlineData(MessageBoxButtons.Ok, new[] { "OK" })]
    [InlineData(MessageBoxButtons.OkCancel, new[] { "Cancel", "OK" })]
    [InlineData(MessageBoxButtons.YesNo, new[] { "No", "Yes" })]
    [InlineData(MessageBoxButtons.YesNoCancel, new[] { "Cancel", "No", "Yes" })]
    public void The_buttons_follow_the_set(MessageBoxButtons set, string[] texts)
    {
        var dialog = MessageBox.Create("Sure?", "Title", set, MessageBoxIcon.None);

        Assert.Equal(texts, dialog.ButtonRow.Children.Cast<Button>().Select(button => button.Text));
    }

    [Fact]
    public void Each_line_of_the_text_is_its_own_TextBlock_and_the_icon_comes_first()
    {
        var dialog = MessageBox.Create("one\r\ntwo", "T", MessageBoxButtons.Ok, MessageBoxIcon.Error);
        var content = (Box)dialog.Content!;
        var icon = Assert.IsType<Icon>(content.Children[0]);
        var lines = (Box)content.Children[1];

        Assert.Equal("error", icon.Glyph);
        Assert.Contains("messagebox-error", icon.Classes);
        Assert.Equal(["one", "two"], lines.Children.Cast<TextBlock>().Select(line => line.Text));
    }

    [Fact]
    public void No_icon_leaves_only_the_text()
    {
        var content = (Box)MessageBox.Create("x", "", MessageBoxButtons.Ok, MessageBoxIcon.None).Content!;

        Assert.Single(content.Children);
    }

    [Fact]
    public async Task Pressing_a_button_completes_the_task_with_its_answer()
    {
        var (root, layer, router) = Setup();
        var task = MessageBox.Show(root, "Sure?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        layer.Arrange(new Size(400, 300));

        var (x, y) = FindButton(layer, "Yes");

        router.Move(x, y);
        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(DialogResult.Yes, await task);
    }

    [Fact]
    public async Task Escape_answers_cancel_when_there_is_a_cancel_button_and_ok_for_an_ok_box()
    {
        var (root, layer, _) = Setup();
        var cancel = MessageBox.Show(root, "a", "", MessageBoxButtons.OkCancel);

        layer.Arrange(new Size(400, 300));
        layer.CloseTop();

        Assert.Equal(DialogResult.Cancel, await cancel);

        var ok = MessageBox.Show(root, "a");

        layer.Arrange(new Size(400, 300));
        layer.CloseTop();

        Assert.Equal(DialogResult.Ok, await ok);
    }

    [Fact]
    public async Task A_box_that_cannot_be_shown_answers_none()
    {
        var detached = new Box();

        Assert.Equal(DialogResult.None, await MessageBox.Show(detached, "a"));
    }
}
