using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class SliderTests
{
    static (Box Root, Slider Slider, PointerRouter Router) Setup()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100 };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "padding": "10px", "align-items": "start" }, "Slider": { "width": "116px", "height": "24px" } } }""";

        root.Children.Add(slider);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(200, 100));

        return (root, slider, new PointerRouter(root));
    }

    [Fact]
    public void The_value_is_clamped_to_the_range_and_raises_the_event_only_when_it_changes()
    {
        var slider = new Slider { Minimum = 10, Maximum = 20 };
        var raised = 0;

        slider.ValueChanged += (_, _) => raised++;

        slider.Value = 99;
        slider.Value = 20;

        Assert.Equal(20, slider.Value);
        Assert.Equal(1, raised);

        slider.Maximum = 15;

        Assert.Equal(15, slider.Value);
    }

    [Fact]
    public void Pressing_on_the_track_moves_the_thumb_and_dragging_follows_the_pointer()
    {
        var (_, slider, router) = Setup();

        Assert.True(router.Down(10 + 8 + 50, 20));
        Assert.Equal(50, slider.Value, 6);
        Assert.Contains("active", slider.PseudoStates);

        Assert.True(router.Move(10 + 8 + 75, 90));
        Assert.Equal(75, slider.Value, 6);
    }

    [Fact]
    public void Dragging_past_the_ends_clamps_and_releasing_ends_the_drag()
    {
        var (_, slider, router) = Setup();

        router.Down(10 + 8 + 50, 20);
        router.Move(500, 20);

        Assert.Equal(100, slider.Value);

        router.Up(500, 20);

        Assert.DoesNotContain("active", slider.PseudoStates);

        router.Move(10, 20);

        Assert.Equal(100, slider.Value);
    }

    [Fact]
    public void The_slider_paints_its_fill_up_to_the_thumb()
    {
        var (root, slider, _) = Setup();
        var fill = new Color(1, 2, 3);

        slider.SetValue(Slider.FillColorProperty, fill);
        slider.Value = 50;

        var builder = new DisplayListBuilder();

        root.Render(builder);

        var rects = builder.Build().Commands.OfType<FillRectCommand>().Where(c => c.Color == fill).ToList();

        Assert.Equal(50, rects.Single().Rect.Width);
    }

    [Fact]
    public void Local_positions_undo_the_scrolling_of_the_ancestors()
    {
        var content = new Box { Name = "content" };
        var viewer = new ScrollViewer { Name = "viewer" };
        var slider = new Slider();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "#content": { "height": "200px" }, "#viewer": { "height": "50px" } } }""";

        content.Children.Add(slider);
        viewer.Content = content;
        root.Children.Add(viewer);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(200, 100));

        var before = slider.ToLocal(30, 30).Y;

        viewer.ScrollBy(20);

        Assert.Equal(before + 20, slider.ToLocal(30, 30).Y);
    }
}
