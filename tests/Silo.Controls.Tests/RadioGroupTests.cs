using Silo;
using Xunit;

public class RadioGroupTests
{
    [Fact]
    public void Choosing_a_button_turns_the_others_off_and_sets_the_index()
    {
        var group = new RadioGroup();
        var small = group.Add("Small");
        var large = group.Add("Large");
        var raised = 0;

        group.SelectionChanged += (_, _) => raised++;

        small.IsChecked = true;
        large.IsChecked = true;

        Assert.False(small.IsChecked);
        Assert.Equal(1, group.SelectedIndex);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void The_index_can_be_set_and_cleared()
    {
        var group = new RadioGroup();

        group.Add("A");
        group.Add("B");

        group.SelectedIndex = 1;

        Assert.Equal(1, group.SelectedIndex);

        group.SelectedIndex = -1;

        Assert.Equal(-1, group.SelectedIndex);
    }

    [Fact]
    public void Two_groups_do_not_exclude_each_other()
    {
        var first = new RadioGroup();
        var second = new RadioGroup();

        first.Add("A").IsChecked = true;
        second.Add("B").IsChecked = true;

        Assert.Equal(0, first.SelectedIndex);
        Assert.Equal(0, second.SelectedIndex);
    }
}
