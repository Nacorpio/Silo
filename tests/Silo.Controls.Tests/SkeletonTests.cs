using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class SkeletonTests
{
    const string Sheet = """
        {
          "rules": {
            ".skeleton-line": { "height": "12px", "width": "160px" },
            ".skeleton-circle": { "width": "40px", "height": "40px" }
          }
        }
        """;

    [Fact]
    public void The_band_crosses_the_block_once_per_period()
    {
        Assert.Equal(-50, Skeleton.BandAt(TimeSpan.Zero, 1000, 100), 3);
        Assert.Equal(25, Skeleton.BandAt(TimeSpan.FromMilliseconds(500), 1000, 100), 3);
        Assert.Equal(-50, Skeleton.BandAt(TimeSpan.FromMilliseconds(1000), 1000, 100), 3);
        Assert.Equal(-50, Skeleton.BandAt(TimeSpan.FromMilliseconds(100), 0, 100), 3);
    }

    [Fact]
    public void It_animates_only_while_active()
    {
        var skeleton = new Skeleton();

        Assert.True(skeleton.IsAnimating);

        skeleton.IsActive = false;

        Assert.False(skeleton.IsAnimating);
    }

    [Fact]
    public void A_stylesheet_sizes_a_line_and_a_circle()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var line = new Skeleton();
        var circle = new Skeleton();

        line.AddClass("skeleton-line");
        circle.AddClass("skeleton-circle");
        root.Children.Add(line);
        root.Children.Add(circle);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        Assert.Equal(12, line.Bounds.Height);
        Assert.Equal(40, circle.Bounds.Width);
        Assert.Equal(40, circle.Bounds.Height);
    }
}
