using Silo;
using Xunit;

public class StepperTests
{
    static Stepper Create()
        => new() { Steps = [new("One", new TextBlock { Text = "1" }), new("Two", new TextBlock { Text = "2" }), new("Three", new TextBlock { Text = "3" })] };

    static Control Header(Stepper stepper) => (Control)stepper.Children[0];

    static Control Body(Stepper stepper) => (Control)stepper.Children[1];

    [Fact]
    public void The_header_marks_done_current_and_upcoming_steps()
    {
        var stepper = Create();

        stepper.CurrentStep = 1;

        var classes = Header(stepper).Children.OfType<Control>().Where(c => c.Classes.Contains("step"))
            .Select(c => c.Classes.Single(k => k is "done" or "current" or "upcoming")).ToList();

        Assert.Equal(["done", "current", "upcoming"], classes);
    }

    [Fact]
    public void The_body_shows_the_content_of_the_current_step_only()
    {
        var stepper = Create();

        Assert.Equal("1", ((TextBlock)Body(stepper).Children.Single()).Text);

        stepper.Next();

        Assert.Equal("2", ((TextBlock)Body(stepper).Children.Single()).Text);
    }

    [Fact]
    public void Next_and_back_stop_at_the_ends_and_raise_the_event_per_move()
    {
        var stepper = Create();
        var raised = 0;

        stepper.StepChanged += (_, _) => raised++;

        Assert.False(stepper.Back());
        Assert.True(stepper.Next());
        Assert.True(stepper.Next());
        Assert.False(stepper.Next());
        Assert.True(stepper.IsLast);
        Assert.True(stepper.Back());
        Assert.Equal(1, stepper.CurrentStep);
        Assert.Equal(3, raised);
    }

    [Fact]
    public void Lines_join_the_steps_and_the_ones_before_the_current_step_are_done()
    {
        var stepper = Create();

        stepper.CurrentStep = 1;

        var lines = Header(stepper).Children.OfType<Control>().Where(c => c.Classes.Contains("step-line")).ToList();

        Assert.Equal(2, lines.Count);
        Assert.Contains("done", lines[0].Classes);
        Assert.DoesNotContain("done", lines[1].Classes);
    }

    [Fact]
    public void An_out_of_range_step_is_clamped_and_an_empty_stepper_is_empty()
    {
        var stepper = Create();

        stepper.CurrentStep = 9;

        Assert.Equal(2, stepper.CurrentStep);

        var empty = new Stepper();

        Assert.False(empty.Next());
        Assert.Empty(Body(empty).Children);
    }
}
