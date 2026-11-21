using Silo.Layout;
using Silo.Styling.Values;
using Xunit;

public class FlexLayoutTests
{
    static Length Px(double value) => TestNode.Px(value);

    [Fact]
    public void Root_without_size_fills_the_available_area()
    {
        var root = new TestNode();

        FlexLayout.Arrange(root, new Size(800, 600));

        Assert.Equal(new Rect(0, 0, 800, 600), root.Rect);
    }

    [Fact]
    public void Row_places_fixed_children_left_to_right_with_gap()
    {
        var a = new TestNode(new LayoutStyle { Width = Px(100), Height = Px(20) });
        var b = new TestNode(new LayoutStyle { Width = Px(50), Height = Px(30) });
        var root = new TestNode(new LayoutStyle { Gap = Px(8) }, null, a, b);

        FlexLayout.Arrange(root, new Size(400, 300));

        Assert.Equal(new Rect(0, 0, 100, 20), a.Rect);
        Assert.Equal(new Rect(108, 0, 50, 30), b.Rect);
    }

    [Fact]
    public void Column_stacks_children_and_stretches_their_width()
    {
        var a = new TestNode(new LayoutStyle { Height = Px(20) });
        var b = new TestNode(new LayoutStyle { Height = Px(30) });
        var root = new TestNode(new LayoutStyle { Direction = FlexDirection.Column }, null, a, b);

        FlexLayout.Arrange(root, new Size(400, 300));

        Assert.Equal(new Rect(0, 0, 400, 20), a.Rect);
        Assert.Equal(new Rect(0, 20, 400, 30), b.Rect);
    }

    [Fact]
    public void Grow_shares_the_free_space_in_proportion()
    {
        var a = new TestNode(new LayoutStyle { Grow = 1 });
        var b = new TestNode(new LayoutStyle { Grow = 3 });
        var root = new TestNode(null, null, a, b);

        FlexLayout.Arrange(root, new Size(400, 100));

        Assert.Equal(100, a.Rect.Width);
        Assert.Equal(300, b.Rect.Width);
        Assert.Equal(100, b.Rect.X);
    }

    [Fact]
    public void Grow_takes_the_space_left_after_fixed_siblings_and_gaps()
    {
        var fixedChild = new TestNode(new LayoutStyle { Width = Px(100) });
        var growing = new TestNode(new LayoutStyle { Grow = 1 });
        var root = new TestNode(new LayoutStyle { Gap = Px(8) }, null, fixedChild, growing);

        FlexLayout.Arrange(root, new Size(400, 100));

        Assert.Equal(292, growing.Rect.Width);
        Assert.Equal(108, growing.Rect.X);
    }

    [Fact]
    public void Shrink_reduces_overflowing_children_in_proportion_to_their_size()
    {
        var a = new TestNode(new LayoutStyle { Width = Px(300) });
        var b = new TestNode(new LayoutStyle { Width = Px(300) });
        var root = new TestNode(null, null, a, b);

        FlexLayout.Arrange(root, new Size(400, 100));

        Assert.Equal(200, a.Rect.Width);
        Assert.Equal(200, b.Rect.Width);
        Assert.Equal(200, b.Rect.X);
    }

    [Fact]
    public void A_max_width_is_respected_and_the_rest_goes_to_the_other_children()
    {
        var a = new TestNode(new LayoutStyle { Grow = 1, MaxWidth = Px(100) });
        var b = new TestNode(new LayoutStyle { Grow = 1 });
        var root = new TestNode(null, null, a, b);

        FlexLayout.Arrange(root, new Size(400, 100));

        Assert.Equal(100, a.Rect.Width);
        Assert.Equal(300, b.Rect.Width);
    }

    [Fact]
    public void A_min_width_stops_shrinking()
    {
        var a = new TestNode(new LayoutStyle { Width = Px(300), MinWidth = Px(250) });
        var b = new TestNode(new LayoutStyle { Width = Px(300) });
        var root = new TestNode(null, null, a, b);

        FlexLayout.Arrange(root, new Size(400, 100));

        Assert.Equal(250, a.Rect.Width);
        Assert.Equal(150, b.Rect.Width);
    }

    [Theory]
    [InlineData(JustifyContent.Start, 0, 60)]
    [InlineData(JustifyContent.Center, 100, 160)]
    [InlineData(JustifyContent.End, 200, 260)]
    [InlineData(JustifyContent.SpaceBetween, 0, 260)]
    [InlineData(JustifyContent.SpaceAround, 50, 210)]
    [InlineData(JustifyContent.SpaceEvenly, 66.66666666666667, 193.33333333333334)]
    public void Justify_content_distributes_free_space(JustifyContent justify, double firstX, double secondX)
    {
        var a = new TestNode(new LayoutStyle { Width = Px(60) });
        var b = new TestNode(new LayoutStyle { Width = Px(40) });
        var root = new TestNode(new LayoutStyle { JustifyContent = justify }, null, a, b);

        FlexLayout.Arrange(root, new Size(300, 100));

        Assert.Equal(firstX, a.Rect.X, 6);
        Assert.Equal(secondX, b.Rect.X, 6);
    }

    [Theory]
    [InlineData(AlignItems.Start, 0)]
    [InlineData(AlignItems.Center, 40)]
    [InlineData(AlignItems.End, 80)]
    public void Align_items_places_children_on_the_cross_axis(AlignItems align, double y)
    {
        var child = new TestNode(new LayoutStyle { Width = Px(50), Height = Px(20) });
        var root = new TestNode(new LayoutStyle { AlignItems = align }, null, child);

        FlexLayout.Arrange(root, new Size(300, 100));

        Assert.Equal(y, child.Rect.Y);
        Assert.Equal(20, child.Rect.Height);
    }

    [Fact]
    public void Align_self_overrides_align_items_and_stretch_fills_the_cross_axis()
    {
        var stretched = new TestNode(new LayoutStyle { Width = Px(50) });
        var centered = new TestNode(new LayoutStyle { Width = Px(50), Height = Px(20), AlignSelf = AlignItems.Center });
        var root = new TestNode(null, null, stretched, centered);

        FlexLayout.Arrange(root, new Size(300, 100));

        Assert.Equal(100, stretched.Rect.Height);
        Assert.Equal(40, centered.Rect.Y);
    }

    [Fact]
    public void Padding_border_and_margin_move_and_shrink_the_content_box()
    {
        var child = new TestNode(new LayoutStyle { Grow = 1, Margin = TestNode.All(5) });
        var root = new TestNode(new LayoutStyle { Padding = TestNode.All(10), Border = TestNode.All(2) }, null, child);

        FlexLayout.Arrange(root, new Size(200, 100));

        Assert.Equal(new Rect(17, 17, 166, 66), child.Rect);
    }

    [Fact]
    public void Leaf_measure_gives_the_natural_size_in_a_row()
    {
        var text = new TestNode(new LayoutStyle { Padding = TestNode.All(4) }, _ => new Size(60, 14));
        var root = new TestNode(new LayoutStyle { AlignItems = AlignItems.Start }, null, text);

        FlexLayout.Arrange(root, new Size(300, 100));

        Assert.Equal(new Rect(0, 0, 68, 22), text.Rect);
    }

    [Fact]
    public void Wrapping_text_in_a_column_gets_its_height_from_the_final_width()
    {
        // Text that needs 200px unwrapped and 10px of height per line at 100px.
        Size Wrap(double? width) => width is null or >= 200 ? new Size(200, 10) : new Size(width.Value, Math.Ceiling(200 / width.Value) * 10);

        var text = new TestNode(null, Wrap);
        var root = new TestNode(new LayoutStyle { Direction = FlexDirection.Column }, null, text);

        FlexLayout.Arrange(root, new Size(100, 300));

        Assert.Equal(new Rect(0, 0, 100, 20), text.Rect);
    }

    [Fact]
    public void Container_without_size_wraps_around_its_children()
    {
        var inner = new TestNode(new LayoutStyle { Gap = Px(4), AlignItems = AlignItems.Start }, null,
            new TestNode(new LayoutStyle { Width = Px(30), Height = Px(10) }),
            new TestNode(new LayoutStyle { Width = Px(20), Height = Px(16) }));
        var root = new TestNode(new LayoutStyle { AlignItems = AlignItems.Start }, null, inner);

        FlexLayout.Arrange(root, new Size(300, 100));

        Assert.Equal(new Rect(0, 0, 54, 16), inner.Rect);
    }

    [Fact]
    public void Percent_sizes_resolve_against_the_parent_content_box()
    {
        var child = new TestNode(new LayoutStyle { Width = new Length(25, LengthUnit.Percent) });
        var root = new TestNode(new LayoutStyle { Padding = TestNode.All(10) }, null, child);

        FlexLayout.Arrange(root, new Size(220, 100));

        Assert.Equal(50, child.Rect.Width);
    }
}
