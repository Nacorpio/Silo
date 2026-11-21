using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ContextMenuTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "width": "300px", "height": "200px" },
            "ContextMenu": { "item-height": "20px" }
          }
        }
        """;

    static (Box Root, ContextMenu Menu, OverlayLayer Layer, PointerRouter Router, List<string> Log) Setup()
    {
        var log = new List<string>();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        var menu = new ContextMenu
        {
            Entries = [new MenuItem("Copy", () => log.Add("copy")), new MenuItem("Paste", () => log.Add("paste")), new("Idle")],
        };

        root.Overlays = layer;
        root.ContextMenu = menu;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(300, 200));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        return (root, menu, layer, new PointerRouter(root, layer), log);
    }

    static void Layout(OverlayLayer layer) => layer.Arrange(new Size(300, 200));

    [Fact]
    public void A_secondary_press_opens_the_menu_at_the_pointer_with_the_size_of_its_entries()
    {
        var (_, menu, layer, router, _) = Setup();

        Assert.True(router.SecondaryDown(40, 30));
        Layout(layer);

        Assert.True(menu.IsOpen);
        Assert.Equal(new Rect(40, 30, 5 * 7 + 16, 3 * 20), menu.Bounds);
    }

    [Fact]
    public void The_menu_is_moved_to_lie_inside_the_window()
    {
        var (_, menu, layer, router, _) = Setup();

        router.SecondaryDown(290, 190);
        Layout(layer);

        Assert.Equal(300 - menu.Bounds.Width, menu.Bounds.X);
        Assert.Equal(200 - menu.Bounds.Height, menu.Bounds.Y);
    }

    [Fact]
    public void Hovering_highlights_a_row_and_pressing_it_closes_the_menu_and_runs_the_entry()
    {
        var (_, menu, layer, router, log) = Setup();

        router.SecondaryDown(40, 30);
        Layout(layer);

        router.Move(45, 30 + 20 + 5);

        Assert.Equal(1, menu.SelectedIndex);

        router.Down(45, 30 + 20 + 5);

        Assert.False(menu.IsOpen);
        Assert.Equal(["paste"], log);
    }

    [Fact]
    public void A_press_outside_closes_the_menu_without_running_an_entry()
    {
        var (_, menu, layer, router, log) = Setup();

        router.SecondaryDown(40, 30);
        Layout(layer);
        router.Down(5, 5);

        Assert.False(menu.IsOpen);
        Assert.Empty(log);
    }

    [Fact]
    public void A_secondary_press_outside_closes_it_and_does_not_open_a_new_one()
    {
        var (_, menu, layer, router, _) = Setup();

        router.SecondaryDown(40, 30);
        Layout(layer);
        router.SecondaryDown(5, 5);

        Assert.False(menu.IsOpen);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void An_entry_without_an_action_just_closes_the_menu()
    {
        var (_, menu, layer, router, log) = Setup();

        router.SecondaryDown(40, 30);
        Layout(layer);
        router.Down(45, 30 + 40 + 5);

        Assert.False(menu.IsOpen);
        Assert.Empty(log);
    }

    [Fact]
    public void An_empty_menu_and_a_Control_without_a_menu_open_nothing()
    {
        var (root, menu, layer, router, _) = Setup();

        menu.Entries = [];

        router.SecondaryDown(40, 30);

        Assert.Equal(0, layer.Count);

        menu.Entries = [new MenuItem("A")];
        root.ContextMenu = null;
        router.SecondaryDown(40, 30);

        Assert.Equal(0, layer.Count);
    }
}
