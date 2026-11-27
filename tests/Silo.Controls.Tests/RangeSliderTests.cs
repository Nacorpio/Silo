using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class RangeSliderTests
{
    const string Sheet = """{ "rules": { "Box": { "align-items": "start" }, "RangeSlider": { "width": "216px", "height": "24px" } } }""";

    static RangeSlider Setup(RangeSlider slider)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(slider);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 100));

        return slider;
    }

    [Fact]
    public void The_thumbs_cannot_cross()
    {
        var slider = new RangeSlider { Minimum = 0, Maximum = 100, Lower = 20, Upper = 60 };

        slider.Lower = 80;

        Assert.Equal(60, slider.Lower);

        slider.Upper = 10;

        Assert.Equal(60, slider.Upper);
    }

    [Fact]
    public void Values_snap_to_the_step_from_the_minimum()
    {
        var slider = new RangeSlider { Minimum = 5, Maximum = 105, Step = 10 };

        slider.Lower = 17;
        slider.Upper = 92;

        Assert.Equal(15, slider.Lower);
        Assert.Equal(95, slider.Upper);
    }

    [Fact]
    public void A_press_moves_the_nearest_thumb()
    {
        var slider = Setup(new RangeSlider { Minimum = 0, Maximum = 100, Lower = 20, Upper = 80 });

        // The track runs from x = 8 to x = 208, so x = 108 is the value 50.
        slider.PointerDown(new(188, 10));

        Assert.Equal(90, slider.Upper);
        Assert.Equal(20, slider.Lower);

        slider.PointerUp(default);
        slider.PointerDown(new(28, 10));

        Assert.Equal(10, slider.Lower);
    }

    [Fact]
    public void Dragging_a_thumb_stops_at_the_other_one()
    {
        var slider = Setup(new RangeSlider { Minimum = 0, Maximum = 100, Lower = 20, Upper = 80 });

        slider.PointerDown(new(8 + 40, 10));
        slider.PointerDrag(new(8 + 190, 10));

        Assert.Equal(80, slider.Lower);
        Assert.Equal(80, slider.Upper);
    }

    [Fact]
    public void The_keys_move_the_active_thumb_and_up_switches_to_the_upper()
    {
        var slider = new RangeSlider { Minimum = 0, Maximum = 100, Step = 5, Lower = 10, Upper = 50 };

        Assert.True(slider.KeyDown(Key.Right));
        Assert.Equal(15, slider.Lower);

        Assert.True(slider.KeyDown(Key.Up));
        Assert.True(slider.IsUpperActive);

        slider.KeyDown(Key.Left);
        slider.KeyDown(Key.End);

        Assert.Equal(45 + 55, slider.Upper);
    }

    [Fact]
    public void Changing_the_range_raises_the_event_once_per_change()
    {
        var slider = new RangeSlider { Minimum = 0, Maximum = 10 };
        var count = 0;

        slider.RangeChanged += (_, _) => count++;
        slider.Lower = 3;
        slider.Lower = 3;
        slider.Maximum = 2;

        Assert.Equal(2, count);
        Assert.Equal(2, slider.Upper);
    }
}
