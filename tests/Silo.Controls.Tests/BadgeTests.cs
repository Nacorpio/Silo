using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class BadgeTests
{
    const string Sheet = """{ "rules": { "Badge": { "padding": "2px 6px", "background": "#ff0000" } } }""";

    static (Box Root, Badge Badge) Setup(Badge badge)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(badge);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        return (root, badge);
    }

    [Theory]
    [InlineData(7, "7")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    public void A_count_above_the_maximum_shows_with_a_plus(int count, string text)
    {
        var badge = new Badge { Value = count };

        Assert.Equal(text, badge.Text);
    }

    [Fact]
    public void The_maximum_can_be_changed_and_applies_to_the_count()
    {
        var badge = new Badge { Value = 12 };

        badge.Maximum = 9;

        Assert.Equal("9+", badge.Text);
    }

    [Fact]
    public void A_badge_without_text_paints_nothing_and_takes_no_content_space()
    {
        var (root, badge) = Setup(new Badge());
        var builder = new DisplayListBuilder();

        root.Render(builder);

        Assert.DoesNotContain(builder.Build().Commands, c => c is FillRectCommand { Color.R: 255 });
        Assert.Equal(12, badge.Bounds.Width);

        badge.Value = 5;
        root.Arrange(new Size(300, 100));

        Assert.Equal(7 + 12, badge.Bounds.Width);
    }

    [Fact]
    public void The_severity_is_a_class_and_null_removes_it()
    {
        var badge = new Badge { Severity = Severity.Warning };

        Assert.Contains("warning", badge.Classes);

        badge.Severity = Severity.Error;

        Assert.DoesNotContain("warning", badge.Classes);
        Assert.Contains("error", badge.Classes);

        badge.Severity = null;

        Assert.DoesNotContain("error", badge.Classes);
    }
}
