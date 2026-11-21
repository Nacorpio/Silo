using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;
using Xunit;

public class ToggleTests
{
    static void Click(ToggleButton toggle)
    {
        Assert.True(toggle.PointerDown());

        toggle.PointerUp(true);
    }

    [Fact]
    public void Clicking_a_checkbox_flips_it_sets_the_checked_state_and_raises_the_event()
    {
        var box = new CheckBox { Text = "Remember" };
        var raised = 0;

        box.CheckedChanged += (_, _) => raised++;

        Click(box);

        Assert.True(box.IsChecked);
        Assert.Contains("checked", box.PseudoStates);
        Assert.Equal(1, raised);

        Click(box);

        Assert.False(box.IsChecked);
        Assert.DoesNotContain("checked", box.PseudoStates);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void A_click_that_changes_the_state_asks_for_new_styles()
    {
        var box = new CheckBox();

        box.PointerDown();

        Assert.True(box.PointerUp(true));
    }

    [Fact]
    public void Releasing_outside_does_not_toggle()
    {
        var box = new CheckBox();

        box.PointerDown();
        box.PointerUp(false);

        Assert.False(box.IsChecked);
    }

    [Fact]
    public void Choosing_a_radio_button_turns_off_the_others_in_its_group_only()
    {
        var panel = new Box();
        var small = new RadioButton { Group = "size", IsChecked = true };
        var large = new RadioButton { Group = "size" };
        var other = new RadioButton { Group = "color", IsChecked = true };

        panel.Children.Add(small);
        panel.Children.Add(large);
        panel.Children.Add(other);

        Click(large);

        Assert.True(large.IsChecked);
        Assert.False(small.IsChecked);
        Assert.True(other.IsChecked);

        Click(large);

        Assert.True(large.IsChecked);
    }

    [Fact]
    public void A_checked_box_paints_its_mark_in_the_indicator_colors()
    {
        var box = new CheckBox { Name = "c" };
        var mark = new Color(1, 2, 3);

        box.SetValue(ToggleButton.IndicatorMarkColorProperty, mark);
        box.Arrange(new Size(100, 20));
        box.IsChecked = true;

        var builder = new DisplayListBuilder();

        box.Render(builder);

        Assert.Contains(builder.Build().Commands, c => c is FillRectCommand { Color: var color } && color == mark);
    }

    [Fact]
    public void An_unchecked_box_does_not_paint_a_mark()
    {
        var box = new CheckBox();
        var mark = new Color(1, 2, 3);

        box.SetValue(ToggleButton.IndicatorMarkColorProperty, mark);
        box.Arrange(new Size(100, 20));

        var builder = new DisplayListBuilder();

        box.Render(builder);

        Assert.DoesNotContain(builder.Build().Commands, c => c is FillRectCommand { Color: var color } && color == mark);
    }

    [Fact]
    public void A_toggle_switch_moves_its_thumb_to_the_right_when_on()
    {
        var toggle = new ToggleSwitch();
        var thumb = new Color(9, 9, 9);

        toggle.SetValue(ToggleButton.IndicatorMarkColorProperty, thumb);
        toggle.Arrange(new Size(100, 20));
        toggle.IsChecked = true;

        var builder = new DisplayListBuilder();

        toggle.Render(builder);

        var rect = builder.Build().Commands.OfType<FillRectCommand>().Single(c => c.Color == thumb).Rect;

        Assert.True(rect.X > 16);
    }

    [Fact]
    public void A_progress_bar_clamps_its_value_and_fills_that_share_of_its_width()
    {
        var bar = new ProgressBar { Value = 5 };

        Assert.Equal(1, bar.Value);

        bar.Value = 0.25;
        bar.SetValue(ProgressBar.BarColorProperty, new Color(7, 7, 7));
        bar.SetValue(Control.HeightProperty, new Length(8, LengthUnit.Px));
        bar.Arrange(new Size(200, 100));

        var builder = new DisplayListBuilder();

        bar.Render(builder);

        var fill = builder.Build().Commands.OfType<FillRectCommand>().Single(c => c.Color == new Color(7, 7, 7)).Rect;

        Assert.Equal(50, fill.Width);
    }
}
