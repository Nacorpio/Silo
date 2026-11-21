using Silo.Styling;
using Xunit;

public class SelectorTests
{
    [Theory]
    [InlineData("Button", 0, 0, 1)]
    [InlineData(".a.b", 0, 2, 0)]
    [InlineData("#x", 1, 0, 0)]
    [InlineData("Button.primary:hover", 0, 2, 1)]
    [InlineData("Box > Button", 0, 0, 2)]
    [InlineData("Button::part(icon)", 0, 0, 2)]
    public void Specificity_follows_css(string text, int ids, int classes, int types)
    {
        var s = Selector.Parse(text);
        Assert.Equal((ids, classes, types), (s.Specificity.Ids, s.Specificity.Classes, s.Specificity.Types));
    }

    [Fact]
    public void Matches_type_class_and_state()
    {
        var n = new TestNode("Button", classes: ["primary"], states: ["hover"]);

        Assert.True(Selector.Parse("Button.primary:hover").Matches(n));
        Assert.False(Selector.Parse("Button.primary:pressed").Matches(n));
        Assert.False(Selector.Parse("Box").Matches(n));
    }

    [Fact]
    public void Child_and_descendant_combinators()
    {
        var box = new TestNode("Box", classes: ["card"]);
        var mid = new TestNode("Box", parent: box);
        var btn = new TestNode("Button", parent: mid);

        Assert.True(Selector.Parse(".card Button").Matches(btn));
        Assert.False(Selector.Parse(".card > Button").Matches(btn));
        Assert.True(Selector.Parse("Box > Button").Matches(btn));
    }

    [Fact]
    public void Part_selector_exposes_part_name_and_matches_the_owner_compound()
    {
        var s = Selector.Parse("Button.primary::part(icon)");

        Assert.Equal("icon", s.Part);
        Assert.True(s.Matches(new TestNode("Button", classes: ["primary"])));
    }

    [Fact]
    public void Round_trips_to_string()
    {
        var selector = Selector.Parse("Box>Button.primary:hover::part(icon)");

        Assert.Equal("Box > Button.primary:hover::part(icon)", selector.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Button..x")]
    [InlineData("> Button")]
    [InlineData("Button::part()")]
    public void Rejects_malformed(string text) => Assert.Throws<FormatException>(() => Selector.Parse(text));
}
