using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ToolbarTests
{
    [Fact]
    public void Buttons_run_their_action_and_separators_are_vertical()
    {
        var toolbar = new Toolbar();
        var clicked = 0;
        var cut = toolbar.AddButton("Cut", () => clicked++);
        var divider = toolbar.AddSeparator();

        toolbar.AddButton("Paste");
        cut.PointerDown();
        cut.PointerUp(true);

        Assert.Equal(1, clicked);
        Assert.Equal(3, toolbar.Children.Count);
        Assert.Contains("vertical", divider.Classes);
    }

    [Fact]
    public void Toolbar_buttons_lay_out_in_a_row_with_the_toolbar_gap()
    {
        var toolbar = new Toolbar { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Toolbar": { "flex-direction": "row", "gap": "4px" }, "Button": { "padding": "0px" } } }""";
        var first = toolbar.AddButton("ab");
        var second = toolbar.AddButton("cd");

        toolbar.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        toolbar.Arrange(new Size(200, 40));

        Assert.Equal(first.Bounds.Right + 4, second.Bounds.X);
    }
}
