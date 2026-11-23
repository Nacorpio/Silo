using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class EmptyStateTests
{
    [Fact]
    public void Only_the_parts_that_are_set_are_in_the_tree()
    {
        var empty = new EmptyState();

        Assert.Empty(empty.Children);

        empty.Title = "Nothing here";

        Assert.Single(empty.Children);

        empty.Glyph = "search";
        empty.Description = "Add something.";
        empty.Action = new Button { Text = "Add" };

        Assert.Equal(["empty-icon", "empty-title", "empty-description", "empty-action"],
            empty.Children.Cast<Control>().Select(child => child.Classes.Last()));

        empty.Title = "";

        Assert.Equal(3, empty.Children.Count);
    }

    [Fact]
    public void Replacing_the_action_removes_the_old_one()
    {
        var first = new Button { Text = "One" };
        var second = new Button { Text = "Two" };
        var empty = new EmptyState { Action = first };

        empty.Action = second;

        Assert.DoesNotContain("empty-action", first.Classes);
        Assert.Contains("empty-action", second.Classes);
        Assert.Single(empty.Children);
        Assert.Same(second, empty.Children[0]);
    }

    [Fact]
    public void The_parts_are_centered_in_a_column()
    {
        const string sheet = """
            {
              "rules": {
                "EmptyState": { "flex-direction": "column", "align-items": "center", "width": "200px", "height": "100px" },
                ".empty-icon": { "width": "20px", "height": "20px" }
              }
            }
            """;

        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var empty = new EmptyState { Glyph = "info", Title = "Hi" };

        root.Children.Add(empty);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        var icon = (Control)empty.Children[0];
        var title = (Control)empty.Children[1];

        Assert.Equal(empty.Bounds.Width / 2, icon.Bounds.X + (icon.Bounds.Width / 2), 1);
        Assert.Equal(empty.Bounds.Width / 2, title.Bounds.X + (title.Bounds.Width / 2), 1);
        Assert.True(title.Bounds.Y > icon.Bounds.Y);
    }
}
