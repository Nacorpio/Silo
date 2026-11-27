using Silo;
using Silo.Studio;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class AnimationPlaygroundTests
{
    [Fact]
    public void The_transition_text_follows_the_chosen_properties_curve_and_timing()
    {
        var playground = new AnimationPlayground
        {
            Properties = PlaygroundProperties.Move,
            Curve = PlaygroundCurve.EaseOut,
            DurationMilliseconds = 300,
            DelayMilliseconds = 50
        };

        Assert.Contains("transform", playground.TransitionText);
        Assert.Contains("300ms", playground.TransitionText);
        Assert.Contains("ease-out", playground.TransitionText);
        Assert.Contains("50ms", playground.TransitionText);
        Assert.True(TransitionList.TryParse(playground.TransitionText, out _));
    }

    [Fact]
    public void The_curve_plot_samples_start_at_zero_and_end_at_one()
    {
        var samples = EasingCurve.Sample(new Motion(TimeSpan.FromMilliseconds(400), Easing.EaseInOut), 20);

        Assert.Equal(0, samples[0].Value, 3);
        Assert.Equal(1, samples[^1].Value, 3);
    }

    [Fact]
    public void The_scaled_clock_runs_slower_and_stands_still_without_jumping()
    {
        var source = new ManualAnimationClock();
        var clock = new ScaledAnimationClock(() => source) { Speed = 0.5 };

        _ = clock.Now;
        source.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(50), clock.Now);

        clock.IsPaused = true;
        source.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(50), clock.Now);

        clock.IsPaused = false;
        clock.Speed = 1;
        source.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Equal(TimeSpan.FromMilliseconds(60), clock.Now);
    }

    [Fact]
    public void The_animations_category_shows_the_playground()
    {
        var matrix = new ThemeMatrix();

        matrix.ShowCategory(ThemeMatrix.AnimationsCategory);

        Assert.Same(matrix.Playground, matrix.Page.Children.Single());
    }
}
