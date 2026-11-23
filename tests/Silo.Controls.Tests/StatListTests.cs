using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class StatListTests
{
    static (Box Root, StatList List) Setup(string rules = "")
    {
        var list = new StatList { Name = "s" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        list.Items = [new("activity", Label: "Activity"), new("star", "1", "star"), new("eye", "0", "watching")];
        root.Children.Add(list);

        var sheet = $$"""{ "rules": { "Box": { "align-items": "start" }, "StatRow": { "flex-direction": "row", "align-items": "center", "gap": "6px" }, "#s": { "stat-gap": "10px", "icon-size": "20px", "value-color": "#ff0000" {{rules}} } } }""";

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 300));

        return (root, list);
    }

    static Control Row(StatList list, int index) => (Control)list.Children[index];

    [Fact]
    public void Each_item_becomes_a_row_with_an_icon_a_bold_value_and_a_label()
    {
        var (_, list) = Setup();

        var row = Row(list, 1);

        Assert.IsType<Icon>(row.Children[0]);
        Assert.Equal("1", ((TextBlock)row.Children[1]).Text);
        Assert.Contains("value", ((Control)row.Children[1]).Classes);
        Assert.Equal("star", ((TextBlock)row.Children[2]).Text);
        Assert.Contains("label", ((Control)row.Children[2]).Classes);
    }

    [Fact]
    public void A_row_without_a_value_has_only_an_icon_and_text()
    {
        var (_, list) = Setup();

        Assert.Equal(2, Row(list, 0).Children.Count);
    }

    [Fact]
    public void The_rows_are_stacked_with_the_stat_gap_between_them()
    {
        var (_, list) = Setup();

        Assert.Equal(Row(list, 0).Bounds.Bottom + 10, Row(list, 1).Bounds.Y);
        Assert.Equal(Row(list, 1).Bounds.Bottom + 10, Row(list, 2).Bounds.Y);
    }

    [Fact]
    public void Icon_size_and_value_color_reach_the_rows()
    {
        var (_, list) = Setup();

        var icon = (Icon)Row(list, 1).Children[0];
        var value = (TextBlock)Row(list, 1).Children[1];

        Assert.Equal(20, icon.GetValue(Icon.IconSizeProperty));
        Assert.Equal(new Silo.Styling.Values.Color(255, 0, 0), value.GetValue(Control.ColorProperty));
    }

    [Fact]
    public void Clicking_a_row_raises_ItemClicked_with_its_index()
    {
        var (_, list) = Setup();
        var clicked = new List<int>();

        list.ItemClicked += (_, index) => clicked.Add(index);

        var row = (Button)Row(list, 2);

        row.PointerDown();
        row.PointerUp(inside: true);

        Assert.Equal([2], clicked);
    }

    [Fact]
    public void An_image_replaces_the_glyph()
    {
        var list = new StatList { Items = [new("star", "1", "star", new FakeImage(10, 10))] };

        Assert.IsType<Image>(Row(list, 0).Children[0]);
    }
}
