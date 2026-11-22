using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ContextMenuSubmenuTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "width": "400px", "height": "300px" },
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
            Entries =
            [
                new MenuItem("&Copy", () => log.Add("copy")),
                MenuItem.Separator,
                new MenuItem("&Recent")
                {
                    Children = [new MenuItem("a.txt", () => log.Add("a")), new MenuItem("b.txt", () => log.Add("b"))],
                },
                new MenuItem("E&xit", () => log.Add("exit")),
            ],
        };

        root.Overlays = layer;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        return (root, menu, layer, new PointerRouter(root, layer), log);
    }

    static void Layout(OverlayLayer layer) => layer.Arrange(new Size(400, 300));

    static (OverlayLayer Layer, ContextMenu Menu, PointerRouter Router, List<string> Log) Open()
    {
        var (root, menu, layer, router, log) = Setup();

        menu.Show(root, 50, 40);
        Layout(layer);

        return (layer, menu, router, log);
    }

    [Fact]
    public void The_text_of_an_entry_does_not_show_the_ampersand()
    {
        var (_, menu, _, _) = Open();

        Assert.Equal(["Copy", "", "Recent", "Exit"], menu.Items);
    }

    [Fact]
    public void A_separator_cannot_be_selected_and_the_arrow_keys_skip_it()
    {
        var (_, menu, _, _) = Open();

        menu.KeyDown(Key.Down);

        Assert.Equal(0, menu.SelectedIndex);

        menu.KeyDown(Key.Down);

        Assert.Equal(2, menu.SelectedIndex);

        menu.SelectedIndex = 1;

        Assert.Equal(-1, menu.SelectedIndex);
    }

    [Fact]
    public void Hovering_an_entry_with_children_opens_its_submenu_to_the_right_of_the_row()
    {
        var (layer, menu, router, _) = Open();

        router.Move(60, 40 + 4 + (2 * 20) + 5);
        Layout(layer);

        Assert.Equal(2, layer.Count);
        Assert.Equal(2, menu.SelectedIndex);
    }

    [Fact]
    public void Moving_to_another_entry_closes_the_submenu()
    {
        var (layer, _, router, _) = Open();

        router.Move(60, 40 + 4 + (2 * 20) + 5);
        router.Move(60, 40 + 4 + 5);

        Assert.Equal(1, layer.Count);
    }

    [Fact]
    public void Pressing_an_entry_of_the_submenu_runs_it_and_closes_every_menu()
    {
        var (layer, menu, router, log) = Open();

        router.Move(60, 40 + 4 + (2 * 20) + 5);
        Layout(layer);

        var x = menu.Bounds.Right + 10;
        var y = menu.Bounds.Y + 4 + (2 * 20) + 5;

        router.Move(x, y);
        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(["a"], log);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void The_pointer_can_go_back_to_the_parent_menu_while_the_submenu_is_open()
    {
        var (layer, menu, router, log) = Open();

        router.Move(60, 40 + 4 + (2 * 20) + 5);
        Layout(layer);

        router.Move(60, 40 + 4 + (3 * 20) + 5);
        router.Down(60, 40 + 4 + (3 * 20) + 5);
        router.Up(60, 40 + 4 + (3 * 20) + 5);

        Assert.Equal(["exit"], log);
        Assert.Equal(0, layer.Count);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void A_press_outside_closes_the_submenu_and_its_parent()
    {
        var (layer, _, router, _) = Open();

        router.Move(60, 40 + 4 + (2 * 20) + 5);
        Layout(layer);

        router.Down(380, 280);
        router.Up(380, 280);

        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void Escape_closes_only_the_submenu()
    {
        var (layer, menu, router, _) = Open();

        router.Move(60, 40 + 4 + (2 * 20) + 5);

        Assert.True(layer.CloseTop());
        Assert.Equal(1, layer.Count);
        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void Right_opens_the_submenu_with_its_first_entry_highlighted_and_Left_closes_it()
    {
        var (layer, menu, _, _) = Open();

        menu.KeyDown(Key.Down);
        menu.KeyDown(Key.Down);

        Assert.True(menu.KeyDown(Key.Right));
        Assert.Equal(2, layer.Count);

        Assert.True(layer.KeyDown(Key.Left, KeyModifiers.None));
        Assert.Equal(1, layer.Count);
    }

    [Fact]
    public void Keys_go_to_the_top_menu_of_the_chain()
    {
        var (layer, menu, _, log) = Open();

        menu.KeyDown(Key.Down);
        menu.KeyDown(Key.Down);
        menu.KeyDown(Key.Right);

        layer.KeyDown(Key.Down, KeyModifiers.None);
        layer.KeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(["b"], log);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void An_access_key_picks_its_entry()
    {
        var (layer, menu, _, log) = Open();

        menu.KeyDown(Key.X);

        Assert.Equal(["exit"], log);
        Assert.Equal(0, layer.Count);
    }

    [Fact]
    public void An_access_key_on_an_entry_with_children_opens_its_submenu()
    {
        var (layer, menu, _, _) = Open();

        menu.KeyDown(Key.R);

        Assert.Equal(2, layer.Count);
    }

    [Fact]
    public void Enter_on_an_entry_with_children_opens_the_submenu_with_its_first_entry_highlighted()
    {
        var (layer, menu, _, log) = Open();

        menu.KeyDown(Key.Down);
        menu.KeyDown(Key.Down);
        menu.KeyDown(Key.Enter);

        Assert.Equal(2, layer.Count);

        layer.KeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(["a"], log);
    }

    [Fact]
    public void A_double_ampersand_is_a_literal_ampersand_and_a_trailing_one_is_kept()
    {
        var menu = new ContextMenu
        {
            Entries = [new MenuItem("Tom && Jerry"), new MenuItem("End&"), new MenuItem("Plain")],
        };

        Assert.Equal(["Tom & Jerry", "End&", "Plain"], menu.Items);
    }
}
