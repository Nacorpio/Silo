using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class AspectRatioBoxTests
{
    const string Sheet = """
        {
          "rules": {
            "AspectRatioBox": { "width": "200px", "height": "200px", "padding": "10px" }
          }
        }
        """;

    static (AspectRatioBox Box, Control Content) Setup(double ratio, double width = 200, double height = 200)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var content = new Box();
        var box = new AspectRatioBox { Ratio = ratio, Content = content };

        root.Children.Add(box);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet.Replace("200px\", \"height\": \"200px", $"{width}px\", \"height\": \"{height}px"), "t.jsonc")]));
        root.Arrange(new Size(400, 400));

        return (box, content);
    }

    [Fact]
    public void Fit_finds_the_biggest_centered_rectangle()
    {
        var wide = AspectRatioBox.Fit(new Rect(0, 0, 100, 100), 2);
        var tall = AspectRatioBox.Fit(new Rect(10, 20, 100, 50), 0.5);

        Assert.Equal(new Rect(0, 25, 100, 50), wide);
        Assert.Equal(new Rect(47.5, 20, 25, 50), tall);
    }

    [Fact]
    public void A_wide_ratio_leaves_bars_above_and_below()
    {
        var (box, content) = Setup(2);

        Assert.Equal(180, content.Bounds.Width, 3);
        Assert.Equal(90, content.Bounds.Height, 3);
        Assert.Equal(box.Bounds.X + 10, content.Bounds.X, 3);
        Assert.Equal(box.Bounds.Y + 10 + 45, content.Bounds.Y, 3);
    }

    [Fact]
    public void A_tall_ratio_leaves_bars_on_the_sides()
    {
        var (box, content) = Setup(0.5);

        Assert.Equal(90, content.Bounds.Width, 3);
        Assert.Equal(180, content.Bounds.Height, 3);
        Assert.Equal(box.Bounds.X + 10 + 45, content.Bounds.X, 3);
    }

    [Fact]
    public void The_content_follows_when_the_box_is_not_square()
    {
        var (_, content) = Setup(1, 300, 100);

        Assert.Equal(80, content.Bounds.Width, 3);
        Assert.Equal(80, content.Bounds.Height, 3);
    }

    [Fact]
    public void An_invalid_ratio_is_ignored_and_replacing_the_content_swaps_it()
    {
        var box = new AspectRatioBox { Ratio = 2 };

        box.Ratio = 0;
        box.Ratio = double.NaN;

        Assert.Equal(2, box.Ratio);

        var first = new Box();
        var second = new Box();

        box.Content = first;
        box.Content = second;

        Assert.Single(box.Children);
        Assert.Same(second, box.Children[0]);
    }
}
