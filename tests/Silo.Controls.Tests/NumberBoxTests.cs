using Silo;
using Xunit;

public class NumberBoxTests
{
    [Fact]
    public void It_starts_at_zero_and_only_accepts_what_can_become_a_number()
    {
        var box = new NumberBox { Minimum = -10 };

        Assert.Equal("0", box.Text);

        Assert.True(box.TextInput("1"));
        Assert.True(box.TextInput("."));
        Assert.False(box.TextInput("."));
        Assert.False(box.TextInput("x"));
        Assert.Equal("01.", box.Text);

        box.KeyDown(Key.Home);

        Assert.True(box.TextInput("-"));
        Assert.False(box.TextInput("-"));
    }

    [Fact]
    public void A_minus_sign_is_refused_when_the_minimum_is_not_negative()
    {
        var box = new NumberBox { Minimum = 0 };

        Assert.False(box.TextInput("-"));
    }

    [Fact]
    public void Value_follows_the_typed_text_and_keeps_its_last_number_while_the_text_is_not_one()
    {
        var box = new NumberBox { Minimum = -10, Maximum = 100 };
        var raised = 0;

        box.ValueChanged += (_, _) => raised++;

        box.KeyDown(Key.End);
        box.KeyDown(Key.Backspace);

        Assert.Equal("", box.Text);
        Assert.Equal(0, box.Value);

        box.TextInput("-");

        Assert.Equal(0, box.Value);

        box.TextInput("5");

        Assert.Equal(-5, box.Value);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_typed_number_is_limited_and_Enter_rewrites_the_text_from_the_value()
    {
        var box = new NumberBox { Maximum = 50 };

        box.TextInput("99");

        Assert.Equal(50, box.Value);
        Assert.Equal("099", box.Text);

        box.KeyDown(Key.Enter);

        Assert.Equal("50", box.Text);
    }

    [Fact]
    public void The_arrows_step_the_value_and_stop_at_the_limits()
    {
        var box = new NumberBox { Minimum = 0, Maximum = 1, Step = 0.5, Value = 0.5 };

        Assert.True(box.KeyDown(Key.Up));
        Assert.Equal("1", box.Text);

        box.KeyDown(Key.Up);

        Assert.Equal(1, box.Value);

        box.KeyDown(Key.Down);
        box.KeyDown(Key.Down);
        box.KeyDown(Key.Down);

        Assert.Equal("0", box.Text);
    }

    [Fact]
    public void Setting_the_value_raises_the_event_once_and_narrowing_the_range_pulls_the_value_in()
    {
        var box = new NumberBox();
        var raised = 0;

        box.ValueChanged += (_, _) => raised++;
        box.Value = 7;
        box.Value = 7;

        Assert.Equal(1, raised);

        box.Maximum = 3;

        Assert.Equal(3, box.Value);
        Assert.Equal("3", box.Text);
        Assert.Equal(2, raised);
    }
}
