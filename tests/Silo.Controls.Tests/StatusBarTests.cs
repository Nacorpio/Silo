using Silo;
using Xunit;

public class StatusBarTests
{
    [Fact]
    public void It_has_three_areas_in_order_with_classes()
    {
        var bar = new StatusBar();

        Assert.Equal(3, bar.Children.Count);
        Assert.Same(bar.Left, bar.Children[0]);
        Assert.Same(bar.Center, bar.Children[1]);
        Assert.Same(bar.Right, bar.Children[2]);
        Assert.Contains("left", bar.Left.Classes);
        Assert.Contains("right", bar.Right.Classes);
    }

    [Fact]
    public void Items_go_to_the_left_area_unless_another_is_given()
    {
        var bar = new StatusBar();

        var ready = bar.AddItem("Ready");
        var position = bar.AddItem("Ln 1", bar.Right);

        Assert.Same(ready, bar.Left.Children[0]);
        Assert.Same(position, bar.Right.Children[0]);
        Assert.Contains("item", ready.Classes);
    }
}
