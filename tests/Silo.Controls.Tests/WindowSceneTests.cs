using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class WindowSceneTests
{
    sealed class Probe : Box
    {
        public List<Key> Keys { get; } = [];

        public override bool KeyDown(Key key, KeyModifiers modifiers)
        {
            Keys.Add(key);

            return true;
        }
    }

    const string Sheet = """
        {
          "rules": {
            "Box": { "align-items": "start" },
            "Button": { "padding": "5px" },
            "TextBox": { "width": "100px", "padding": "4px" },
            "ProgressRing": { "width": "20px", "height": "20px" }
          }
        }
        """;

    static readonly Size Window = new(400, 300);

    static (WindowScene Scene, ManualAnimationClock Clock, Box Root) Setup(params Control[] content)
    {
        var clock = new ManualAnimationClock();
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        foreach (var control in content)
            root.Children.Add(control);

        var scene = new WindowScene(root, clock) { Styles = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]) };

        scene.Render(Window);

        return (scene, clock, root);
    }

    [Fact]
    public void A_new_scene_needs_a_frame_and_rendering_one_clears_it()
    {
        var (scene, _, _) = Setup();

        Assert.False(scene.IsDirty);

        scene.Invalidate();

        Assert.True(scene.IsDirty);
        Assert.NotNull(scene.Render(Window));
        Assert.False(scene.IsDirty);
    }

    [Fact]
    public void A_window_without_area_renders_nothing_and_still_clears_the_flag()
    {
        var (scene, _, _) = Setup();

        scene.Invalidate();

        Assert.Null(scene.Render(new Size(0, 300)));
        Assert.False(scene.IsDirty);
    }

    [Fact]
    public void The_scene_gives_the_root_its_overlays_and_the_clock()
    {
        var (scene, clock, root) = Setup();

        Assert.Same(scene.Overlays, root.Overlays);
        Assert.Same(clock, root.AnimationClock);
    }

    [Fact]
    public void Moving_over_a_button_sets_hover_and_asks_for_a_frame()
    {
        var button = new Button { Text = "Go" };
        var (scene, _, _) = Setup(button);

        Assert.True(scene.PointerMoved(button.Bounds.X + 2, button.Bounds.Y + 2));
        Assert.Contains("hover", button.PseudoStates);
        Assert.True(scene.IsDirty);
        Assert.True(scene.PointerLeft());
        Assert.DoesNotContain("hover", button.PseudoStates);
    }

    [Fact]
    public void Pressing_and_releasing_over_a_button_clicks_it()
    {
        var button = new Button { Text = "Go" };
        var (scene, _, _) = Setup(button);
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        var (x, y) = (button.Bounds.X + 2, button.Bounds.Y + 2);

        scene.PointerPressed(x, y, KeyModifiers.None);
        scene.PointerReleased(x, y);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void Pressing_a_text_box_gives_it_the_keyboard_and_typed_text()
    {
        var box = new TextBox();
        var (scene, _, _) = Setup(box);

        scene.PointerPressed(box.Bounds.X + 2, box.Bounds.Y + 2, KeyModifiers.None);

        Assert.True(scene.TextInput("hi"));
        Assert.Equal("hi", box.Text);
    }

    [Fact]
    public void Escape_closes_the_top_overlay()
    {
        var (scene, _, _) = Setup();
        var menu = new Probe();

        scene.Overlays.Show(menu, 10, 10);

        Assert.True(scene.KeyDown(Key.Escape, KeyModifiers.None));
        Assert.Equal(0, scene.Overlays.Count);
        Assert.Empty(menu.Keys);
    }

    [Fact]
    public void A_modal_overlay_takes_the_keys_and_the_focused_control_does_not()
    {
        var box = new TextBox();
        var (scene, _, _) = Setup(box);
        var menu = new Probe();

        scene.Focus(box);
        scene.Overlays.Show(menu, 10, 10);

        scene.KeyDown(Key.Left, KeyModifiers.None);

        Assert.Equal([Key.Left], menu.Keys);
        Assert.Equal(0, box.CaretIndex);
    }

    [Fact]
    public void A_press_while_a_modal_overlay_is_shown_does_not_move_the_keyboard()
    {
        var box = new TextBox();
        var other = new TextBox();
        var (scene, _, _) = Setup(box, other);

        scene.Focus(box);
        scene.Overlays.Show(new Probe(), 300, 200);
        scene.PointerPressed(other.Bounds.X + 2, other.Bounds.Y + 2, KeyModifiers.None);

        Assert.Contains("focus", box.PseudoStates);
        Assert.DoesNotContain("focus", other.PseudoStates);
    }

    [Fact]
    public void A_tooltip_shows_after_its_delay_and_goes_when_the_pointer_leaves()
    {
        var button = new Button { Text = "Go", Tooltip = "Run it" };
        var (scene, clock, _) = Setup(button);

        scene.PointerMoved(button.Bounds.X + 2, button.Bounds.Y + 2);

        Assert.NotNull(scene.DueIn());
        Assert.Equal(0, scene.Overlays.Count);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(scene.Tick());
        Assert.Equal(1, scene.Overlays.Count);

        scene.PointerLeft();

        Assert.Equal(0, scene.Overlays.Count);
    }

    [Fact]
    public void Nothing_is_due_when_nothing_waits()
    {
        var (scene, _, _) = Setup();

        Assert.Null(scene.DueIn());
    }

    [Fact]
    public void While_a_control_animates_a_frame_is_due_each_frame_time()
    {
        var ring = new ProgressRing { IsActive = true };
        var (scene, _, _) = Setup(ring);

        Assert.Equal(TimeSpan.FromMilliseconds(Animations.FrameMilliseconds), scene.DueIn());
        Assert.True(scene.Tick());
    }

    [Fact]
    public void A_toast_runs_out_when_time_passes()
    {
        var (scene, clock, _) = Setup();

        scene.Toasts.Show("Saved");
        scene.Tick();

        Assert.Single(scene.Toasts.Toasts);

        clock.Advance(scene.Toasts.Duration + TimeSpan.FromSeconds(1));

        Assert.True(scene.Tick());
        Assert.Empty(scene.Toasts.Toasts);
    }

    [Fact]
    public void Changing_the_styles_applies_them_and_asks_for_a_frame()
    {
        var button = new Button { Text = "Go" };
        var (scene, _, _) = Setup(button);

        scene.Styles = new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "Box": { "align-items": "start" }, "Button": { "padding": "20px" } } }""", "u.jsonc")]);

        Assert.True(scene.IsDirty);

        scene.Render(Window);

        Assert.Equal((20 * 2) + 14, button.Bounds.Height);
    }
}
