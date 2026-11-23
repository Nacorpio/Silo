using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class SearchBoxTests
{
    static (Box Root, SearchBox Box, PointerRouter Router) Setup()
    {
        var box = new SearchBox();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "padding": "10px", "align-items": "start" }, "SearchBox": { "width": "100px" } } }""";

        root.Children.Add(box);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        return (root, box, new PointerRouter(root));
    }

    [Fact]
    public void It_asks_for_a_search_while_empty_and_draws_the_clear_button_only_with_text()
    {
        var (root, box, _) = Setup();
        var builder = new DisplayListBuilder();

        root.Render(builder);

        Assert.Equal(["Search"], builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text));

        box.TextInput("ab");
        builder = new DisplayListBuilder();
        root.Render(builder);

        Assert.Equal(["ab", "×"], builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text));
    }

    [Fact]
    public void Escape_and_the_clear_button_empty_the_text_and_raise_the_event()
    {
        var (_, box, router) = Setup();
        var raised = 0;

        box.TextChanged += (_, _) => raised++;
        box.TextInput("ab");

        Assert.True(box.KeyDown(Key.Escape));
        Assert.Equal("", box.Text);
        Assert.False(box.KeyDown(Key.Escape));

        box.TextInput("cd");

        Assert.True(router.Down(10 + 95, 10 + 5));
        Assert.Equal("", box.Text);
        Assert.Equal(4, raised);
    }

    [Fact]
    public void Pressing_the_text_still_moves_the_caret()
    {
        var (_, box, router) = Setup();

        box.TextInput("abcd");
        router.Down(10, 15);

        Assert.Equal(0, box.CaretIndex);
        Assert.Equal("abcd", box.Text);
    }
}
