using Silo.Styling.Values;
using Xunit;

public class TransformTests
{
    [Fact]
    public void None_and_an_empty_text()
    {
        Assert.True(Transform.TryParse("none", out var none));
        Assert.True(none.IsNone);
        Assert.False(Transform.TryParse("", out _));
        Assert.False(Transform.TryParse("   ", out _));
    }

    [Fact]
    public void Functions_are_read_and_combined_in_any_order()
    {
        Assert.True(Transform.TryParse("scale(2) translate(10px, 5) rotate(90deg)", out var all));
        Assert.Equal(new Transform(10, 5, 90, 2, 2), all);

        Assert.True(Transform.TryParse("translateX(3px) translateY(4) translate(1px)", out var moves));
        Assert.Equal(new Transform(4, 4, 0, 1, 1), moves);

        Assert.True(Transform.TryParse("scale(2, 3) scaleX(2) scaleY(0.5) rotate(10) rotate(20deg)", out var more));
        Assert.Equal(new Transform(0, 0, 30, 4, 1.5), more);
    }

    [Theory]
    [InlineData("spin(3)")]
    [InlineData("rotate()")]
    [InlineData("rotate(1, 2)")]
    [InlineData("scale(a)")]
    [InlineData("translate(1px, 2px, 3px)")]
    [InlineData("translate(1px")]
    [InlineData("translate(10%)")]
    [InlineData("rotate(NaN)")]
    public void Bad_text_is_rejected(string text)
    {
        Assert.False(Transform.TryParse(text, out var transform));
        Assert.True(transform.IsNone);
    }

    [Fact]
    public void ToString_writes_what_differs_and_reads_back()
    {
        var transform = new Transform(10, 5, 30, 2, 1);

        Assert.Equal("translate(10px, 5px) rotate(30deg) scale(2, 1)", transform.ToString());
        Assert.True(Transform.TryParse(transform.ToString(), out var again));
        Assert.Equal(transform, again);
        Assert.Equal("none", Transform.None.ToString());
    }

    [Fact]
    public void The_matrix_scales_and_rotates_around_the_origin_and_then_moves()
    {
        var (x, y) = new Transform(0, 0, 0, 2, 2).ToMatrix(10, 10).Apply(20, 10);

        Assert.Equal((30, 10), (x, y));

        (x, y) = new Transform(0, 0, 90, 1, 1).ToMatrix(10, 10).Apply(20, 10);

        Assert.Equal(10, x, 6);
        Assert.Equal(20, y, 6);

        (x, y) = new Transform(5, -3, 0, 1, 1).ToMatrix(100, 100).Apply(0, 0);

        Assert.Equal((5, -3), (x, y));
    }

    [Fact]
    public void A_matrix_can_be_inverted_unless_it_is_flattened()
    {
        var matrix = new Transform(7, 2, 25, 1.5, 0.5).ToMatrix(4, 9);
        var (x, y) = matrix.Apply(3, 8);

        Assert.True(matrix.TryInvert(out var inverse));

        var (backX, backY) = inverse.Apply(x, y);

        Assert.Equal(3, backX, 6);
        Assert.Equal(8, backY, 6);

        Assert.False(new Transform(0, 0, 0, 0, 1).ToMatrix(0, 0).TryInvert(out var none));
        Assert.True(none.IsIdentity);
    }

    [Theory]
    [InlineData("center", 50, 50)]
    [InlineData("left top", 0, 0)]
    [InlineData("top left", 0, 0)]
    [InlineData("right bottom", 100, 100)]
    [InlineData("top", 50, 0)]
    [InlineData("left", 0, 50)]
    [InlineData("25% 75%", 25, 75)]
    public void The_origin_reads_keywords_in_any_order(string text, double x, double y)
    {
        Assert.True(TransformOrigin.TryParse(text, out var origin));
        Assert.Equal((x, y), origin.Resolve(100, 100));
    }

    [Fact]
    public void The_origin_resolves_pixels_and_percentages_against_the_box()
    {
        Assert.True(TransformOrigin.TryParse("10px 25%", out var origin));
        Assert.Equal((10, 20), origin.Resolve(200, 80));
        Assert.False(TransformOrigin.TryParse("up", out _));
        Assert.False(TransformOrigin.TryParse("1px 2px 3px", out _));
        Assert.False(TransformOrigin.TryParse("", out _));
    }
}
