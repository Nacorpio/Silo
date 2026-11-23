using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class AvatarTests
{
    const string Sheet = """{ "rules": { "Avatar": { "width": "40px", "height": "40px", "border-radius": "20px", "color": "#ffffff" } } }""";

    static (Box Root, Avatar Avatar) Setup(Avatar avatar)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(avatar);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(200, 100));

        return (root, avatar);
    }

    static List<DrawCommand> Frame(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands];
    }

    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("grace", "GR")]
    [InlineData("  linus   torvalds  ", "LT")]
    [InlineData("x", "X")]
    [InlineData("", "")]
    [InlineData("Åsa Öberg", "ÅÖ")]
    public void The_initials_come_from_the_first_two_words_or_letters(string name, string initials)
    {
        Assert.Equal(initials, new Avatar { DisplayName = name }.Initials);
    }

    [Fact]
    public void Initials_are_drawn_in_the_color_on_a_background_picked_from_the_name()
    {
        var (root, _) = Setup(new Avatar { DisplayName = "Ada Lovelace" });
        var commands = Frame(root);

        Assert.Contains(commands, c => c is ClipRoundRectCommand { CornerRadius: 20 });
        Assert.Single(commands.OfType<DrawTextCommand>(), c => c.Text == "AL");
        Assert.Contains(commands, c => c is FillRectCommand { Rect.Width: 40 });
    }

    [Fact]
    public void The_same_name_always_gets_the_same_color_and_another_name_can_differ()
    {
        var first = Frame(Setup(new Avatar { DisplayName = "Ada" }).Root).OfType<FillRectCommand>().First().Color;
        var again = Frame(Setup(new Avatar { DisplayName = "ada" }).Root).OfType<FillRectCommand>().First().Color;
        var colors = new[] { "Ada", "Bo", "Cy", "Di", "Ed", "Fy" }
            .Select(n => Frame(Setup(new Avatar { DisplayName = n }).Root).OfType<FillRectCommand>().First().Color)
            .Distinct();

        Assert.Equal(first, again);
        Assert.True(colors.Count() > 1);
    }

    [Fact]
    public void A_picture_covers_the_avatar_and_hides_the_initials()
    {
        var (root, _) = Setup(new Avatar { DisplayName = "Ada", Source = new FakeImage(80, 40) });
        var commands = Frame(root);
        var image = commands.OfType<DrawImageCommand>().Single();

        Assert.Equal(80, image.Destination.Width);
        Assert.Equal(40, image.Destination.Height);
        Assert.Equal(-20, image.Destination.X);
        Assert.DoesNotContain(commands, c => c is DrawTextCommand);
    }
}
