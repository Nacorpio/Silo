using Silo;
using Xunit;

public class ExpanderTests
{
    [Fact]
    public void The_content_is_not_in_the_tree_until_the_Expander_is_expanded()
    {
        var content = new TextBlock { Text = "More" };
        var expander = new Expander { Header = "Advanced", Content = content };

        Assert.Single(expander.Children);
        Assert.Null(content.Parent);

        expander.IsExpanded = true;

        Assert.Equal(2, expander.Children.Count);
        Assert.Same(expander, content.Parent);
        Assert.Contains("expanded", expander.PseudoStates);

        expander.IsExpanded = false;

        Assert.Single(expander.Children);
        Assert.DoesNotContain("expanded", expander.PseudoStates);
    }

    [Fact]
    public void Pressing_the_header_toggles_the_Expander_and_raises_the_event()
    {
        var expander = new Expander { Header = "Advanced", Content = new TextBlock() };
        var header = (Button)expander.Children[0];
        var raised = 0;

        expander.ExpandedChanged += (_, _) => raised++;

        header.PointerDown();
        header.PointerUp(true);

        Assert.True(expander.IsExpanded);
        Assert.Equal(1, raised);
        Assert.Equal("v Advanced", header.Text);

        header.PointerDown();
        header.PointerUp(true);

        Assert.False(expander.IsExpanded);
        Assert.Equal("> Advanced", header.Text);
    }

    [Fact]
    public void Content_set_while_expanded_is_added_and_the_old_content_is_removed()
    {
        var first = new TextBlock();
        var second = new TextBlock();
        var expander = new Expander { Content = first, IsExpanded = true };

        expander.Content = second;

        Assert.Null(first.Parent);
        Assert.Same(expander, second.Parent);
        Assert.Equal(2, expander.Children.Count);
    }
}
