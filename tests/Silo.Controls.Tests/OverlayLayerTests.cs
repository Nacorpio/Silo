using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class OverlayLayerTests
{
    const string Sheet = """{ "rules": { "Box": { "width": "80px", "height": "40px" }, "Button": { "padding": "4px" } } }""";

    static (OverlayLayer Layer, Box Root, PointerRouter Router, StyleResolver Resolver) Setup()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        return (layer, root, new PointerRouter(root, layer), resolver);
    }

    static void Layout(OverlayLayer layer, StyleResolver resolver)
    {
        layer.ApplyStyles(resolver);
        layer.Arrange(new Size(400, 300));
    }

    [Fact]
    public void A_shown_Control_sits_at_its_position_with_its_own_size()
    {
        var (layer, _, _, resolver) = Setup();
        var menu = new Box();

        layer.Show(menu, 120, 60);
        Layout(layer, resolver);

        Assert.True(layer.Contains(menu));
        Assert.Equal(1, layer.Count);
        Assert.Equal(new Rect(120, 60, 80, 40), menu.Bounds);
    }

    [Fact]
    public void A_Control_that_sticks_out_is_moved_into_the_window_as_far_as_it_fits()
    {
        var (layer, _, _, resolver) = Setup();
        var menu = new Box();
        var huge = new Box();

        huge.SetValue(Control.WidthProperty, new Silo.Styling.Values.Length(500, Silo.Styling.Values.LengthUnit.Px));

        layer.Show(menu, 390, 290);
        layer.Show(huge, 50, 50);
        Layout(layer, resolver);

        Assert.Equal(new Rect(320, 260, 80, 40), menu.Bounds);
        Assert.Equal(0, huge.Bounds.X);
    }

    [Fact]
    public void Showing_and_closing_raise_Changed_and_closing_frees_the_Control()
    {
        var (layer, _, _, _) = Setup();
        var menu = new Box();
        var changes = 0;

        layer.Changed += (_, _) => changes++;

        layer.Show(menu, 0, 0);

        Assert.True(layer.Close(menu));
        Assert.False(layer.Close(menu));
        Assert.Null(menu.Parent);
        Assert.Equal(2, changes);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void A_Control_cannot_be_shown_twice()
    {
        var (layer, _, _, _) = Setup();
        var menu = new Box();

        layer.Show(menu, 0, 0);

        Assert.Throws<InvalidOperationException>(() => layer.Show(menu, 5, 5));
    }

    [Fact]
    public void Hit_testing_finds_the_overlay_then_the_backdrop_and_nothing_when_empty()
    {
        var (layer, _, _, resolver) = Setup();
        var menu = new Box();

        Assert.Null(layer.HitTest(130, 70));

        layer.Show(menu, 120, 60);
        Layout(layer, resolver);

        Assert.Same(menu, layer.HitTest(130, 70));
        Assert.True(layer.IsBackdrop(layer.HitTest(10, 10)));
        Assert.False(layer.IsBackdrop(menu));
    }

    [Fact]
    public void A_press_outside_closes_the_top_overlay_and_is_not_passed_on()
    {
        var (layer, root, router, resolver) = Setup();
        var clicked = 0;
        var under = new Button { Text = "Under" };
        var menu = new Box();

        under.Click += (_, _) => clicked++;

        root.Children.Add(under);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Show(menu, 300, 200);
        Layout(layer, resolver);

        Assert.True(router.Down(under.Bounds.X + 3, under.Bounds.Y + 3));
        Assert.False(layer.Contains(menu));

        router.Up(under.Bounds.X + 3, under.Bounds.Y + 3);

        Assert.Equal(0, clicked);
    }

    [Fact]
    public void A_press_on_the_overlay_keeps_it_open_and_reaches_its_Controls()
    {
        var (layer, _, router, resolver) = Setup();
        var menu = new Box();
        var item = new Button { Text = "Copy" };
        var clicked = 0;

        item.Click += (_, _) => clicked++;

        menu.Children.Add(item);

        layer.Show(menu, 100, 100);
        Layout(layer, resolver);

        var x = 100 + item.Bounds.X + 3;
        var y = 100 + item.Bounds.Y + 3;

        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(1, clicked);
        Assert.True(layer.Contains(menu));
    }

    [Fact]
    public void Hover_ignores_the_window_behind_an_overlay()
    {
        var (layer, root, router, resolver) = Setup();
        var under = new Button { Text = "Under" };

        root.Children.Add(under);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Show(new Box(), 300, 200);
        Layout(layer, resolver);

        router.Move(under.Bounds.X + 3, under.Bounds.Y + 3);

        Assert.Null(router.Hovered);
    }

    [Fact]
    public void CloseTop_closes_the_last_shown_and_CloseAll_closes_every_overlay()
    {
        var (layer, _, _, _) = Setup();
        var first = new Box();
        var second = new Box();

        layer.Show(first, 0, 0);
        layer.Show(second, 0, 0);

        Assert.True(layer.CloseTop());
        Assert.True(layer.Contains(first));
        Assert.False(layer.Contains(second));
        Assert.True(layer.CloseAll());
        Assert.False(layer.CloseAll());
    }

    [Fact]
    public void Rendering_paints_the_overlays_in_the_order_they_were_shown()
    {
        var (layer, _, _, resolver) = Setup();
        var first = new Box();
        var second = new Box();
        var builder = new DisplayListBuilder();

        first.SetValue(Control.BackgroundProperty, new Silo.Styling.Values.Color(1, 0, 0));
        second.SetValue(Control.BackgroundProperty, new Silo.Styling.Values.Color(2, 0, 0));

        layer.Show(first, 0, 0);
        layer.Show(second, 10, 10);
        Layout(layer, resolver);
        layer.Render(builder);

        var fills = builder.Build().Commands.OfType<FillRectCommand>().Select(c => c.Color.R).ToList();

        Assert.Equal([1, 2], fills);
    }

    [Fact]
    public void A_passive_overlay_is_only_drawn_and_never_takes_the_pointer_or_a_press()
    {
        var (layer, root, router, resolver) = Setup();
        var under = new Button { Text = "Under" };
        var tip = new Box();
        var clicked = 0;

        under.Click += (_, _) => clicked++;

        root.Children.Add(under);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Show(tip, under.Bounds.X, under.Bounds.Y, modal: false);
        Layout(layer, resolver);

        Assert.Equal(1, layer.Count);
        Assert.Equal(0, layer.ModalCount);
        Assert.Null(layer.HitTest(under.Bounds.X + 3, under.Bounds.Y + 3));
        Assert.False(layer.CloseTop());

        router.Down(under.Bounds.X + 3, under.Bounds.Y + 3);
        router.Up(under.Bounds.X + 3, under.Bounds.Y + 3);

        Assert.Equal(1, clicked);
        Assert.True(layer.Contains(tip));
        Assert.True(layer.CloseAll());
    }

    [Fact]
    public void Only_modal_overlays_count_for_the_top_one_and_the_backdrop()
    {
        var (layer, _, _, resolver) = Setup();
        var menu = new Box();

        layer.Show(menu, 10, 10);
        layer.Show(new Box(), 200, 200, modal: false);
        Layout(layer, resolver);

        Assert.Equal(2, layer.Count);
        Assert.Equal(1, layer.ModalCount);
        Assert.Same(menu, layer.HitTest(12, 12));
        Assert.True(layer.IsBackdrop(layer.HitTest(205, 205)));
        Assert.True(layer.CloseTop());
        Assert.Equal(1, layer.Count);
    }
}
