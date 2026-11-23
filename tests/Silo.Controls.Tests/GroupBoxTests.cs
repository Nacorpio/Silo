using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class GroupBoxTests
{
    const string Sheet = """
        { "rules": {
          "GroupBox": { "flex-direction": "column", "padding": "10px", "border-width": "1px" },
          "Card": { "flex-direction": "column", "padding": "10px" }
        } }
        """;

    static Box Arrange(Control control)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(control);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 300));

        return root;
    }

    [Fact]
    public void A_group_box_stacks_the_header_above_the_content()
    {
        var content = new TextBlock { Text = "Body" };
        var group = new GroupBox { Header = "Options", Content = content };

        Arrange(group);

        Assert.True(group.HeaderControl.Bounds.Y + group.HeaderControl.Bounds.Height <= content.Bounds.Y);
        Assert.Contains("header", group.HeaderControl.Classes);
    }

    [Fact]
    public void An_empty_header_is_not_part_of_the_group_box()
    {
        var group = new GroupBox { Content = new TextBlock { Text = "Body" } };

        Assert.Single(group.Children);
    }

    [Fact]
    public void A_card_stacks_header_content_and_footer_in_that_order()
    {
        var header = new TextBlock { Text = "H" };
        var content = new TextBlock { Text = "C" };
        var footer = new TextBlock { Text = "F" };
        var card = new Card { Footer = footer, Header = header, Content = content };

        Arrange(card);

        Assert.True(header.Bounds.Y < content.Bounds.Y);
        Assert.True(content.Bounds.Y < footer.Bounds.Y);
        Assert.Contains("footer", footer.Classes);
    }

    [Fact]
    public void Replacing_a_part_removes_the_old_one_and_its_class()
    {
        var first = new TextBlock { Text = "1" };
        var card = new Card { Content = first };

        card.Content = new TextBlock { Text = "2" };

        Assert.Single(card.Children);
        Assert.DoesNotContain("content", first.Classes);
    }
}
