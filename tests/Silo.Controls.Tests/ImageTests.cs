using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class ImageTests
{
    static List<DrawImageCommand> Render(Control root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands.OfType<DrawImageCommand>().ToList();
    }

    static Box Arranged(Image image, string sheet = """{ "rules": { "Box": { "align-items": "start" } } }""")
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(image);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        return root;
    }

    [Fact]
    public void Without_a_size_it_is_as_large_as_the_picture_and_draws_it_there()
    {
        var picture = new FakeImage(40, 30);
        var image = new Image { Source = picture };
        var root = Arranged(image);

        Assert.Equal(new Rect(0, 0, 40, 30), image.Bounds);
        Assert.Equal([new DrawImageCommand(picture, new Rect(0, 0, 40, 30))], Render(root));
    }

    [Fact]
    public void With_a_size_the_picture_is_scaled_to_fit_and_centered_without_distortion()
    {
        var picture = new FakeImage(40, 20);
        var image = new Image { Source = picture };
        var root = Arranged(image, """{ "rules": { "Box": { "align-items": "start" }, "Image": { "width": "100px", "height": "100px" } } }""");

        Assert.Equal([new DrawImageCommand(picture, new Rect(0, 25, 100, 50))], Render(root));
    }

    [Fact]
    public void A_mask_image_is_drawn_in_the_text_color_instead_of_as_a_picture()
    {
        var picture = new FakeImage(16, 16);
        var image = new Image { Source = picture, IsMask = true };
        var root = Arranged(image, """{ "rules": { "Box": { "align-items": "start" }, "Image": { "color": "#ff0000" } } }""");
        var builder = new DisplayListBuilder();

        root.Render(builder);

        var commands = builder.Build().Commands;

        Assert.Empty(commands.OfType<DrawImageCommand>());
        Assert.Equal([new DrawMaskCommand(picture, new Rect(0, 0, 16, 16), new Color(255, 0, 0))], commands.OfType<DrawMaskCommand>());
    }

    [Fact]
    public void Without_a_source_it_has_no_size_and_draws_nothing()
    {
        var image = new Image();
        var root = Arranged(image);

        Assert.Equal(new Rect(0, 0, 0, 0), image.Bounds);
        Assert.Empty(Render(root));
    }
}
