using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class PointerHandlerTests
{
    class Probe : Box, IPointerHandler
    {
        public List<string> Calls { get; } = [];

        public PointerEvent? Last { get; private set; }

        public bool Changes { get; set; }

        public bool PointerDown(PointerEvent e) => Record("down", e);

        public bool PointerDrag(PointerEvent e) => Record("drag", e);

        public bool PointerUp(PointerEvent e) => Record("up", e);

        public bool PointerMove(PointerEvent e) => Record("move", e);

        public bool PointerLeave() => Record("leave", null);

        bool Record(string call, PointerEvent? e)
        {
            Calls.Add(call);

            Last = e ?? Last;

            return Changes;
        }
    }

    sealed class InnerProbe : Probe;

    const string Sheet = """
        {
          "rules": {
            "Box": { "align-items": "start" },
            "Probe": { "width": "100px", "height": "100px", "align-items": "start" },
            "InnerProbe": { "width": "40px", "height": "40px" },
            "Button": { "padding": "5px" }
          }
        }
        """;

    static (Probe Outer, InnerProbe Inner, PointerRouter Router) Setup(Control? extra = null)
    {
        var outer = new Probe();
        var inner = new InnerProbe();
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        outer.Children.Add(inner);

        if (extra is not null)
            outer.Children.Add(extra);

        root.Children.Add(outer);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(200, 200));

        return (outer, inner, new PointerRouter(root));
    }

    [Fact]
    public void A_press_goes_to_the_innermost_handler_only()
    {
        var (outer, inner, router) = Setup();

        router.Down(20, 20);

        Assert.Equal(["down"], inner.Calls.Where(call => call == "down"));
        Assert.DoesNotContain("down", outer.Calls);
    }

    [Fact]
    public void A_press_on_the_outer_handler_is_relative_to_it()
    {
        var (outer, _, router) = Setup();

        router.Down(70, 60);

        Assert.Equal(new PointerEvent(70, 60), outer.Last);
    }

    [Fact]
    public void The_handler_that_got_the_press_keeps_the_pointer_until_release()
    {
        var (outer, inner, router) = Setup();

        router.Down(20, 20);
        router.Move(150, 150);
        router.Up(150, 150);

        Assert.Equal("drag", inner.Calls[^2]);
        Assert.Equal("up", inner.Calls[^1]);
        Assert.Equal(new PointerEvent(150, 150), inner.Last);
        Assert.DoesNotContain("drag", outer.Calls);
        Assert.DoesNotContain("up", outer.Calls);
    }

    [Fact]
    public void A_move_without_a_press_hovers_the_handler_and_leaves_the_one_before()
    {
        var (outer, inner, router) = Setup();

        router.Move(20, 20);
        router.Move(70, 70);

        Assert.Contains("move", inner.Calls);
        Assert.Equal("leave", inner.Calls[^1]);
        Assert.Equal(["move"], outer.Calls);
    }

    [Fact]
    public void The_pointer_leaving_the_window_leaves_the_handler()
    {
        var (outer, _, router) = Setup();

        router.Move(70, 70);
        router.Leave();

        Assert.Equal("leave", outer.Calls[^1]);
    }

    [Fact]
    public void The_modifier_keys_reach_the_handler()
    {
        var (outer, _, router) = Setup();

        router.Modifiers = KeyModifiers.Shift;
        router.Down(70, 70);

        Assert.Equal(KeyModifiers.Shift, outer.Last?.Modifiers);
    }

    [Fact]
    public void A_handler_that_changed_asks_for_a_new_frame()
    {
        var (outer, _, router) = Setup();

        outer.Changes = true;

        router.Move(70, 70);

        Assert.True(router.Down(70, 70));
        Assert.True(router.Up(70, 70));
    }

    [Fact]
    public void A_button_inside_a_handler_is_still_pressed_and_clicked()
    {
        var button = new Button { Text = "Go" };
        var (outer, _, router) = Setup(button);
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        router.Move(5, 80);

        var x = button.Bounds.X + 2;
        var y = button.Bounds.Y + 2;

        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(1, clicks);
        Assert.Contains("down", outer.Calls);
    }
}
