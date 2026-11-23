using Silo;
using Silo.Layout;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class TransitionTests
{
    const string Sheet = """
        {
          "rules": {
            "#root": { "width": "400px", "height": "300px", "align-items": "flex-start" },
            "#target": { "width": "100px", "height": "50px", "background": "#000000", "transition": "background 100ms linear, width 100ms linear" },
            "#target.on": { "background": "#ffffff", "width": "200px" },
            "#spring": { "opacity": "1", "transition": "opacity spring(170 18)" },
            "#spring.off": { "opacity": "0" },
            "#delayed": { "opacity": "1", "transition": "opacity 100ms linear 50ms" },
            "#delayed.off": { "opacity": "0" },
            "#plain": { "background": "#000000", "flex-direction": "row", "transition": "all 100ms linear" },
            "#plain.on": { "background": "#ffffff", "flex-direction": "column", "width": "50%" },
            "#fade": { "opacity": "1", "transition": "opacity 100ms linear, width 100ms linear" },
            "#fade.off": { "opacity": "0", "width": "50px" }
          }
        }
        """;

    static readonly StyleResolver Resolver = new([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

    static (Box Root, Box Target, ManualAnimationClock Clock) Setup(string name = "target", bool withClock = true)
    {
        var clock = new ManualAnimationClock();
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var target = new Box { Name = name };

        if (withClock)
            root.AnimationClock = clock;

        root.Children.Add(target);
        root.ApplyStyles(Resolver);
        root.Arrange(new Size(400, 300));

        return (root, target, clock);
    }

    static void Restyle(Box root, Box target, string? add = null, string? remove = null)
    {
        if (add is not null)
            target.AddClass(add);

        if (remove is not null)
            target.RemoveClass(remove);

        root.ApplyStyles(Resolver);
        root.Arrange(new Size(400, 300));
    }

    static void Step(Box root, int milliseconds)
    {
        Animations.Advance(root, TimeSpan.FromMilliseconds(milliseconds));
        root.Arrange(new Size(400, 300));
    }

    static byte Red(Control control) => control.GetValue(Control.BackgroundProperty).R;

    [Fact]
    public void The_first_style_does_not_transition_from_the_defaults()
    {
        var (_, target, _) = Setup();

        Assert.Equal(0, Red(target));
        Assert.False(target.HasActiveTransitions);

        var clock = new ManualAnimationClock();
        var root = new Box { Name = "root", AnimationClock = clock };
        var late = new Box { Name = "target" };

        late.AddClass("on");
        root.Children.Add(late);
        root.ApplyStyles(Resolver);

        Assert.Equal(255, Red(late));
        Assert.False(late.HasActiveTransitions);
    }

    [Fact]
    public void A_changed_value_moves_to_the_new_one_over_the_duration()
    {
        var (root, target, _) = Setup();

        Restyle(root, target, add: "on");

        Assert.True(target.HasActiveTransitions);
        Assert.True(Animations.IsAnimating(root));
        Assert.Equal(0, Red(target));

        Step(root, 50);

        Assert.InRange(Red(target), 126, 129);

        Step(root, 49);

        Assert.InRange(Red(target), 252, 254);

        Step(root, 1);

        Assert.Equal(255, Red(target));
        Assert.False(target.HasActiveTransitions);
        Assert.False(Animations.IsAnimating(root));
    }

    [Fact]
    public void Layout_follows_an_animated_length()
    {
        var (root, target, _) = Setup();

        Restyle(root, target, add: "on");
        Step(root, 50);

        Assert.InRange(target.Bounds.Width, 149, 151);

        Step(root, 100);

        Assert.Equal(200, target.Bounds.Width);
    }

    [Fact]
    public void Without_a_clock_the_value_changes_at_once()
    {
        var (root, target, _) = Setup(withClock: false);

        Restyle(root, target, add: "on");

        Assert.Equal(255, Red(target));
        Assert.False(target.HasActiveTransitions);
    }

    [Fact]
    public void A_delay_holds_the_old_value_first()
    {
        var (root, target, _) = Setup("delayed");

        Restyle(root, target, add: "off");
        Step(root, 40);

        Assert.Equal(1, target.GetValue(Element.OpacityProperty));

        Step(root, 60);

        Assert.InRange(target.GetValue(Element.OpacityProperty), 0.49, 0.51);

        Step(root, 100);

        Assert.Equal(0, target.GetValue(Element.OpacityProperty));
    }

    [Fact]
    public void A_change_in_the_middle_starts_from_what_is_shown()
    {
        var (root, target, _) = Setup();

        Restyle(root, target, add: "on");
        Step(root, 50);

        var middle = Red(target);

        Restyle(root, target, remove: "on");

        Assert.Equal(middle, Red(target));

        Step(root, 50);

        Assert.InRange(Red(target), (middle / 2) - 2, (middle / 2) + 2);

        Step(root, 50);

        Assert.Equal(0, Red(target));
    }

    [Fact]
    public void A_property_without_a_transition_changes_at_once()
    {
        var (root, target, _) = Setup("plain");

        Restyle(root, target, add: "on");

        Assert.Equal(FlexDirection.Column, target.GetValue(Control.FlexDirectionProperty));
    }

    [Fact]
    public void All_covers_properties_that_can_be_blended_and_skips_the_rest()
    {
        var (root, target, _) = Setup("plain");

        Restyle(root, target, add: "on");

        Assert.Equal(0, Red(target));
        Assert.True(target.HasActiveTransitions);
        Assert.Equal(FlexDirection.Column, target.GetValue(Control.FlexDirectionProperty));

        Step(root, 100);

        Assert.Equal(255, Red(target));
    }

    [Fact]
    public void A_length_without_a_value_or_in_another_unit_changes_at_once()
    {
        var (root, target, _) = Setup("plain");

        Restyle(root, target, add: "on");

        // The width went from auto to 50%, which cannot be blended.
        Assert.Equal(new Length(50, LengthUnit.Percent), target.GetValue(Control.WidthProperty));
    }

    [Fact]
    public void A_value_set_from_C_sharp_transitions_like_a_styled_one()
    {
        var (root, target, _) = Setup();

        target.SetValue(Control.BackgroundProperty, new Color(200, 0, 0));

        Assert.Equal(0, Red(target));

        Step(root, 50);

        Assert.InRange(Red(target), 99, 101);

        target.ClearValue(Control.BackgroundProperty);
        Step(root, 50);

        Assert.InRange(Red(target), 0, 51);

        Step(root, 100);

        Assert.Equal(0, Red(target));
    }

    [Fact]
    public void Reduced_motion_skips_everything_except_opacity()
    {
        var (root, target, _) = Setup("fade");

        root.ReducedMotion = true;

        Restyle(root, target, add: "off");

        Assert.Equal(new Length(50, LengthUnit.Px), target.GetValue(Control.WidthProperty));
        Assert.Equal(1, target.GetValue(Element.OpacityProperty));
        Assert.True(target.HasActiveTransitions);

        Step(root, 50);

        Assert.InRange(target.GetValue(Element.OpacityProperty), 0.49, 0.51);
    }

    [Fact]
    public void A_spring_settles_on_the_target_and_may_overshoot()
    {
        var (root, target, _) = Setup("spring");

        Restyle(root, target, add: "off");

        var seen = new List<double>();

        for (var i = 0; i < 200 && target.HasActiveTransitions; i++)
        {
            Step(root, 16);
            seen.Add(target.GetValue(Element.OpacityProperty));
        }

        Assert.False(target.HasActiveTransitions);
        Assert.Equal(0, target.GetValue(Element.OpacityProperty));
        Assert.True(seen.Count > 5);
        Assert.Contains(seen, value => value < -0.001);
    }

    [Fact]
    public void An_interrupted_spring_keeps_its_speed()
    {
        var (root, target, _) = Setup("spring");

        Restyle(root, target, add: "off");
        Step(root, 100);

        var atTurn = target.GetValue(Element.OpacityProperty);

        Restyle(root, target, remove: "off");
        Step(root, 5);

        // It was heading down fast, so it carries on down for a moment before the new target pulls it back.
        Assert.True(target.GetValue(Element.OpacityProperty) < atTurn);
    }

    [Fact]
    public void The_transition_property_itself_never_transitions_and_is_not_inherited()
    {
        var (root, target, _) = Setup();
        var child = new Box { Name = "child" };

        target.Children.Add(child);
        root.ApplyStyles(Resolver);

        Assert.Empty(child.GetValue(Element.TransitionProperty).Entries);
        Assert.NotEmpty(target.GetValue(Element.TransitionProperty).Entries);
    }

    [Fact]
    public void A_bad_transition_is_reported_and_leaves_the_value_instant()
    {
        const string sheet = """{ "rules": { "#root": { "transition": "background fast" } } }""";

        var root = new Box { Name = "root", AnimationClock = new ManualAnimationClock() };
        var diagnostics = root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));

        Assert.Single(diagnostics);
        Assert.Empty(root.GetValue(Element.TransitionProperty).Entries);
    }

    [Fact]
    public void Advance_needs_a_manual_clock()
    {
        var root = new Box();

        Assert.Throws<InvalidOperationException>(() => Animations.Advance(root, TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void The_scheduler_reports_running_transitions_and_ticks_them()
    {
        var (root, target, clock) = Setup();
        var scheduler = new AnimationScheduler(root);

        Assert.False(scheduler.IsAnimating);

        Restyle(root, target, add: "on");

        Assert.True(scheduler.IsAnimating);

        clock.Advance(TimeSpan.FromMilliseconds(150));

        Assert.False(scheduler.Tick());
        Assert.False(scheduler.IsAnimating);
        Assert.Equal(255, Red(target));
        Assert.Equal(Animations.FrameMilliseconds, scheduler.FrameMilliseconds);

        scheduler.ReducedMotion = true;

        Assert.True(root.ReducedMotion);
        Assert.True(target.ReducedMotion);
    }
}
