using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class ExpanderTests
{
    static void Click(Button button)
    {
        button.PointerDown();
        button.PointerUp(inside: true);
    }

    static (Box Root, Expander Expander) Setup(bool open = false)
    {
        var expander = new Expander { Header = "Advanced", Content = new TextBlock { Text = "More" } };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """
            { "rules": {
                "Box": { "align-items": "start" },
                "Expander": { "flex-direction": "column", "width": "200px" },
                "ExpanderHeader": { "padding": "4px 4px 4px 20px", "indicator-color": "#ff0000" }
            } }
            """;

        root.Children.Add(expander);

        expander.IsExpanded = open;

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        return (root, expander);
    }

    static IReadOnlyList<DrawCommand> Commands(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands;
    }

    [Fact]
    public void A_new_Expander_is_closed_and_only_its_header_is_in_the_tree()
    {
        var expander = new Expander { Header = "Advanced", Content = new TextBlock { Text = "More" } };

        Assert.False(expander.IsExpanded);
        Assert.Single(expander.Children);
        Assert.Same(expander.HeaderControl, expander.Children[0]);
        Assert.Equal("Advanced", expander.Header);
    }

    [Fact]
    public void Clicking_the_header_opens_and_closes_it_and_raises_the_event()
    {
        var expander = new Expander { Content = new TextBlock { Text = "More" } };
        var raised = 0;

        expander.ExpandedChanged += (_, _) => raised++;

        Click(expander.HeaderControl);

        Assert.True(expander.IsExpanded);
        Assert.Equal(2, expander.Children.Count);
        Assert.Contains("expanded", expander.PseudoStates);
        Assert.Contains("expanded", expander.HeaderControl.PseudoStates);

        Click(expander.HeaderControl);

        Assert.False(expander.IsExpanded);
        Assert.Single(expander.Children);
        Assert.DoesNotContain("expanded", expander.PseudoStates);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void A_click_on_the_header_asks_for_a_new_frame()
    {
        var expander = new Expander();

        expander.HeaderControl.PointerDown();

        Assert.True(expander.HeaderControl.PointerUp(inside: true));
    }

    [Fact]
    public void Setting_the_state_to_the_same_value_does_nothing()
    {
        var expander = new Expander();
        var raised = 0;

        expander.ExpandedChanged += (_, _) => raised++;

        expander.IsExpanded = false;

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Content_set_while_open_is_shown_and_replaced_content_leaves_the_tree()
    {
        var expander = new Expander { IsExpanded = true };
        var first = new TextBlock { Text = "One" };
        var second = new TextBlock { Text = "Two" };

        expander.Content = first;

        Assert.Equal(2, expander.Children.Count);

        expander.Content = second;

        Assert.Equal(2, expander.Children.Count);
        Assert.Same(second, expander.Children[1]);
        Assert.Null(first.Parent);

        expander.Content = null;

        Assert.Single(expander.Children);
    }

    [Fact]
    public void The_content_is_drawn_only_while_the_Expander_is_open()
    {
        var (closed, _) = Setup(open: false);
        var (open, _) = Setup(open: true);

        Assert.DoesNotContain(Commands(closed), c => c is DrawTextCommand { Text: "More" });
        Assert.Contains(Commands(open), c => c is DrawTextCommand { Text: "More" });
    }

    [Fact]
    public void The_content_is_below_the_header()
    {
        var (_, expander) = Setup(open: true);

        Assert.True(((Control)expander.Children[1]).Bounds.Y >= expander.HeaderControl.Bounds.Bottom);
    }

    [Fact]
    public void The_triangle_is_drawn_in_the_indicator_color_and_points_differently_when_open()
    {
        var (closed, _) = Setup(open: false);
        var (open, _) = Setup(open: true);

        var red = new Silo.Styling.Values.Color(255, 0, 0);
        var closedTriangle = Commands(closed).OfType<FillRectCommand>().Where(c => c.Color == red).ToList();
        var openTriangle = Commands(open).OfType<FillRectCommand>().Where(c => c.Color == red).ToList();

        Assert.Equal(3, closedTriangle.Count);
        Assert.Equal(3, openTriangle.Count);
        Assert.All(closedTriangle, c => Assert.Equal(2, c.Rect.Width));
        Assert.All(openTriangle, c => Assert.Equal(2, c.Rect.Height));
    }
}
