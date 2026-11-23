using Silo;
using Xunit;

public class SegmentedControlTests
{
    [Fact]
    public void Items_become_segments_with_first_and_last_classes()
    {
        var control = new SegmentedControl { Items = ["Day", "Week", "Month"] };

        Assert.Equal(3, control.Children.Count);
        Assert.Contains("first", ((Control)control.Children[0]).Classes);
        Assert.Contains("last", ((Control)control.Children[2]).Classes);
        Assert.DoesNotContain("first", ((Control)control.Children[1]).Classes);
    }

    [Fact]
    public void Clicking_a_segment_selects_it_and_marks_only_that_one()
    {
        var control = new SegmentedControl { Items = ["A", "B", "C"], SelectedIndex = 0 };
        var raised = 0;

        control.SelectionChanged += (_, _) => raised++;

        var second = (Button)control.Children[1];

        second.PointerDown();
        second.PointerUp(inside: true);

        Assert.Equal(1, control.SelectedIndex);
        Assert.Equal(1, raised);
        Assert.Contains("selected", second.Classes);
        Assert.DoesNotContain("selected", ((Control)control.Children[0]).Classes);
    }

    [Fact]
    public void The_arrow_keys_move_the_selection_and_stop_at_the_ends()
    {
        var control = new SegmentedControl { Items = ["A", "B"], SelectedIndex = 0 };

        Assert.False(control.KeyDown(Key.Left));
        Assert.True(control.KeyDown(Key.Right));
        Assert.Equal(1, control.SelectedIndex);
        Assert.False(control.KeyDown(Key.Right));
        Assert.True(control.KeyDown(Key.Home));
        Assert.Equal(0, control.SelectedIndex);
        Assert.True(control.KeyDown(Key.End));
        Assert.Equal(1, control.SelectedIndex);
    }

    [Fact]
    public void An_index_outside_the_items_selects_nothing()
    {
        var control = new SegmentedControl { Items = ["A"], SelectedIndex = 0 };

        control.SelectedIndex = 5;

        Assert.Equal(-1, control.SelectedIndex);
        Assert.DoesNotContain("selected", ((Control)control.Children[0]).Classes);
    }
}
