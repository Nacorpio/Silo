using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class SplitPaneTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "align-items": "start" },
            "SplitPane": { "width": "300px", "height": "100px", "divider-size": "10px" }
          }
        }
        """;

    static (Box Root, SplitPane Split, Box A, Box B, PointerRouter Router) Setup(Orientation orientation = Orientation.Horizontal, Action<SplitPane>? configure = null)
    {
        var split = new SplitPane { Orientation = orientation };
        var a = new Box();
        var b = new Box();
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        split.AddPane(a, minSize: 50);
        split.AddPane(b, minSize: 50);
        configure?.Invoke(split);

        root.Children.Add(split);
        Layout(root, orientation);

        return (root, split, a, b, new PointerRouter(root));
    }

    static void Layout(Box root, Orientation orientation = Orientation.Horizontal)
    {
        var sheet = orientation == Orientation.Horizontal
            ? Sheet
            : Sheet.Replace("\"width\": \"300px\", \"height\": \"100px\"", "\"width\": \"100px\", \"height\": \"300px\"");

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 400));
    }

    [Fact]
    public void Panes_without_a_size_share_the_room_left_by_the_dividers()
    {
        var (_, _, a, b, _) = Setup();

        Assert.Equal(new Rect(0, 0, 145, 100), a.Bounds);
        Assert.Equal(new Rect(155, 0, 145, 100), b.Bounds);
    }

    [Fact]
    public void A_pane_keeps_the_size_that_was_asked_for_and_the_other_takes_the_rest()
    {
        var split = new SplitPane();
        var a = new Box();
        var b = new Box();
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        split.AddPane(a, size: 100);
        split.AddPane(b);
        root.Children.Add(split);
        Layout(root);

        Assert.Equal(100, a.Bounds.Width);
        Assert.Equal(190, b.Bounds.Width);
    }

    [Fact]
    public void Dragging_a_divider_moves_it_and_gives_one_pane_what_the_other_loses()
    {
        var (root, split, a, b, router) = Setup();

        router.Move(147, 50);
        router.Down(147, 50);
        router.Move(200, 50);
        Layout(root);

        Assert.Equal(198, a.Bounds.Width, 3);
        Assert.Equal(92, b.Bounds.Width, 3);

        router.Up(200, 50);

        router.Move(10, 50);

        Assert.Equal(198, split.SizeOf(0), 3);
    }

    [Fact]
    public void A_drag_stops_at_the_minimum_size_of_the_pane_next_to_the_divider()
    {
        var (root, _, a, b, router) = Setup();

        router.Move(147, 50);
        router.Down(147, 50);
        router.Move(900, 50);
        Layout(root);

        Assert.Equal(240, a.Bounds.Width, 3);
        Assert.Equal(50, b.Bounds.Width, 3);

        router.Move(-500, 50);
        Layout(root);

        Assert.Equal(50, a.Bounds.Width, 3);
    }

    [Fact]
    public void A_press_that_is_not_on_a_divider_is_left_alone()
    {
        var (_, split, _, _, router) = Setup();

        router.Move(20, 50);
        router.Down(20, 50);
        router.Move(100, 50);

        Assert.Equal(-1, split.FocusedDivider);
        Assert.Equal(145, split.SizeOf(0), 3);
    }

    [Fact]
    public void The_panes_keep_their_proportions_when_the_room_changes()
    {
        var (root, split, a, _, _) = Setup(configure: s => s.Move(0, 0));

        split.Move(0, 55);

        Layout(root);

        Assert.Equal(200, a.Bounds.Width, 3);

        var wider = Sheet.Replace("300px", "590px");

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(wider, "t.jsonc")]));
        root.Arrange(new Size(700, 400));

        Assert.Equal(400, a.Bounds.Width, 3);
    }

    [Fact]
    public void A_double_press_collapses_the_pane_before_the_divider_and_a_second_one_brings_it_back()
    {
        var time = 1000L;
        var (root, split, a, b, router) = Setup(configure: s => s.Clock = () => time);

        for (var i = 0; i < 2; i++)
        {
            router.Move(147, 50);
            router.Down(147, 50);
            router.Up(147, 50);

            time += 100;
        }

        Layout(root);

        Assert.True(split.IsCollapsed(0));
        Assert.DoesNotContain(a, split.Children);
        Assert.Equal(290, b.Bounds.Width);
        Assert.Equal(0, split.SizeOf(0));

        time += 1000;

        for (var i = 0; i < 2; i++)
        {
            router.Move(5, 50);
            router.Down(5, 50);
            router.Up(5, 50);

            time += 100;
        }

        Layout(root);

        Assert.False(split.IsCollapsed(0));
        Assert.Contains(a, split.Children);
        Assert.Equal(145, a.Bounds.Width, 3);
        Assert.Equal(145, b.Bounds.Width, 3);
    }

    [Fact]
    public void Two_slow_presses_are_not_a_double_press()
    {
        var time = 0L;
        var (_, split, _, _, router) = Setup(configure: s => s.Clock = () => time);

        router.Move(147, 50);
        router.Down(147, 50);
        router.Up(147, 50);

        time = 5000;

        router.Down(147, 50);
        router.Up(147, 50);

        Assert.False(split.IsCollapsed(0));
    }

    [Fact]
    public void The_arrow_keys_move_the_focused_divider_and_enter_collapses_the_pane()
    {
        var (_, split, _, _, router) = Setup();

        Assert.False(split.KeyDown(Key.Right, KeyModifiers.None));

        router.Move(147, 50);
        router.Down(147, 50);
        router.Up(147, 50);

        Assert.Equal(0, split.FocusedDivider);
        Assert.True(split.KeyDown(Key.Right, KeyModifiers.None));
        Assert.Equal(155, split.SizeOf(0), 3);
        Assert.True(split.KeyDown(Key.Left, KeyModifiers.Shift));
        Assert.Equal(105, split.SizeOf(0), 3);
        Assert.False(split.KeyDown(Key.Up, KeyModifiers.None));
        Assert.True(split.KeyDown(Key.Enter, KeyModifiers.None));
        Assert.True(split.IsCollapsed(0));
    }

    [Fact]
    public void A_vertical_split_stacks_the_panes_and_uses_the_y_position()
    {
        var (root, _, a, b, router) = Setup(Orientation.Vertical);

        Assert.Equal(new Rect(0, 0, 100, 145), a.Bounds);
        Assert.Equal(new Rect(0, 155, 100, 145), b.Bounds);

        router.Move(50, 147);
        router.Down(50, 147);
        router.Move(50, 197);
        Layout(root, Orientation.Vertical);

        Assert.Equal(195, a.Bounds.Height, 3);
    }

    [Fact]
    public void The_last_pane_cannot_be_collapsed_away_when_no_other_pane_is_left()
    {
        var split = new SplitPane();

        split.AddPane(new Box());

        Assert.False(split.Collapse(0));
    }
}

public class SplitPaneLockTests
{
    static (SplitPane Split, PointerRouter Router, Box Root) Setup()
    {
        var split = new SplitPane();
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        split.AddPane(new Box(), minSize: 50);
        split.AddPane(new Box(), minSize: 50);
        root.Children.Add(split);

        var sheet = """{ "rules": { "Box": { "align-items": "start" }, "SplitPane": { "width": "300px", "height": "100px", "divider-size": "10px" } } }""";

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 400));

        return (split, new PointerRouter(root), root);
    }

    [Fact]
    public void A_locked_SplitPane_ignores_drags_double_presses_and_keys()
    {
        var time = 0L;
        var (split, router, _) = Setup();

        split.Clock = () => time;
        split.IsLocked = true;

        for (var i = 0; i < 2; i++)
        {
            router.Move(147, 50);
            router.Down(147, 50);
            router.Move(220, 50);
            router.Up(220, 50);

            time += 100;
        }

        Assert.Equal(145, split.SizeOf(0), 3);
        Assert.False(split.IsCollapsed(0));
        Assert.Equal(-1, split.FocusedDivider);
        Assert.False(split.KeyDown(Key.Right, KeyModifiers.None));
    }

    [Fact]
    public void Locking_ends_a_drag_and_code_can_still_move_a_locked_divider()
    {
        var (split, router, _) = Setup();

        router.Move(147, 50);
        router.Down(147, 50);

        split.IsLocked = true;
        router.Move(220, 50);

        Assert.Equal(145, split.SizeOf(0), 3);
        Assert.True(split.Move(0, 20));
        Assert.Equal(165, split.SizeOf(0), 3);
        Assert.True(split.Collapse(0));
    }

    [Fact]
    public void Unlocking_makes_the_dividers_work_again()
    {
        var (split, router, _) = Setup();

        split.IsLocked = true;
        split.IsLocked = false;

        router.Move(147, 50);
        router.Down(147, 50);
        router.Move(197, 50);

        Assert.Equal(195, split.SizeOf(0), 3);
    }
}
