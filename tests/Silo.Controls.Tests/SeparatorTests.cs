using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class SeparatorTests
{
    static (Box Row, Separator Line) Setup(string sheet, bool vertical)
    {
        var line = new Separator();
        var row = new Box { TextMeasurer = new FixedMeasurer() };

        if (vertical)
            line.AddClass("vertical");

        row.Children.Add(new TextBlock { Text = "A" });
        row.Children.Add(line);

        row.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        row.Arrange(new Size(200, 50));

        return (row, line);
    }

    [Fact]
    public void A_Separator_is_a_line_with_the_thickness_and_color_that_the_theme_gives_it()
    {
        var sheet = """
            { "rules": {
                "Box": { "flex-direction": "column" },
                "Separator": { "background": "#112233", "height": "1px" }
            } }
            """;

        var (_, line) = Setup(sheet, vertical: false);

        Assert.Equal(1, line.Bounds.Height);
        Assert.Equal(200, line.Bounds.Width);
        Assert.Equal(new Color(0x11, 0x22, 0x33), line.GetValue(Control.BackgroundProperty));
    }

    [Fact]
    public void The_vertical_Class_makes_it_as_high_as_the_row_and_one_pixel_wide()
    {
        var sheet = """
            { "rules": {
                "Box": { "flex-direction": "row", "height": "30px" },
                "Separator": { "height": "1px", "&.vertical": { "width": "1px", "height": "auto" } }
            } }
            """;

        var (_, line) = Setup(sheet, vertical: true);

        Assert.Equal(1, line.Bounds.Width);
        Assert.Equal(30, line.Bounds.Height);
    }
}
