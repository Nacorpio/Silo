using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class RatingTests
{
    const string Sheet = """{ "rules": { "Box": { "align-items": "start" }, "Rating": { "icon-size": "20px", "rating-gap": "4px" } } }""";

    static (Box Root, Rating Rating) Setup(Rating rating)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(rating);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        return (root, rating);
    }

    [Fact]
    public void It_is_as_wide_as_its_stars_and_gaps()
    {
        var (_, rating) = Setup(new Rating());

        Assert.Equal((5 * 20) + (4 * 4), rating.Bounds.Width);
    }

    [Fact]
    public void A_click_sets_the_star_under_the_pointer()
    {
        var (_, rating) = Setup(new Rating());

        rating.PointerDown(55, 10);

        Assert.Equal(3, rating.Value);
    }

    [Fact]
    public void With_half_stars_the_left_half_gives_a_half()
    {
        var (_, rating) = Setup(new Rating { AllowHalf = true });

        rating.PointerDown(5, 10);

        Assert.Equal(0.5, rating.Value);

        rating.PointerDown(15, 10);

        Assert.Equal(1, rating.Value);
    }

    [Fact]
    public void Without_half_stars_a_value_is_rounded()
    {
        var rating = new Rating { Value = 2.6 };

        Assert.Equal(3, rating.Value);
    }

    [Fact]
    public void Clicking_the_current_star_clears_only_when_allowed()
    {
        var (_, plain) = Setup(new Rating { Value = 1 });
        var (_, clearing) = Setup(new Rating { Value = 1, AllowClear = true });

        plain.PointerDown(5, 10);
        clearing.PointerDown(5, 10);

        Assert.Equal(1, plain.Value);
        Assert.Equal(0, clearing.Value);
    }

    [Fact]
    public void A_read_only_rating_ignores_the_pointer_and_keys()
    {
        var (_, rating) = Setup(new Rating { Value = 2, IsReadOnly = true });

        Assert.False(rating.PointerDown(90, 10));
        Assert.False(rating.PointerMove(90));
        Assert.False(rating.KeyDown(Key.Right));
        Assert.Equal(2, rating.Value);
    }

    [Fact]
    public void Hovering_previews_the_value_and_leaving_restores_it()
    {
        var (_, rating) = Setup(new Rating { Value = 1 });

        Assert.True(rating.PointerMove(80));
        Assert.Equal(4, rating.DisplayValue);
        Assert.Equal(1, rating.Value);
        Assert.True(rating.PointerLeave());
        Assert.Equal(1, rating.DisplayValue);
    }

    [Fact]
    public void The_keys_change_the_value_within_the_range()
    {
        var rating = new Rating { Value = 4 };

        rating.KeyDown(Key.Right);
        rating.KeyDown(Key.Right);

        Assert.Equal(5, rating.Value);

        rating.KeyDown(Key.Left);

        Assert.Equal(4, rating.Value);

        rating.KeyDown(Key.End);

        Assert.Equal(5, rating.Value);
    }

    [Fact]
    public void Whole_stars_are_filled_and_a_half_star_is_clipped()
    {
        var (root, _) = Setup(new Rating { Value = 1.5, AllowHalf = true });
        var builder = new DisplayListBuilder();

        root.Render(builder);

        var commands = builder.Build().Commands;

        Assert.Equal(2, commands.OfType<FillPolygonCommand>().Count());
        Assert.Equal(5, commands.OfType<StrokePolylineCommand>().Count());
    }
}
