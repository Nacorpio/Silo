using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class MenuBarTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "width": "400px", "height": "300px" },
            "MenuBar": { "flex-direction": "row", "width": "400px", "height": "24px" },
            "MenuBarItem": { "padding": "4px 10px" },
            "ContextMenu": { "item-height": "20px" }
          }
        }
        """;

    static (Box Root, MenuBar Bar, OverlayLayer Layer, PointerRouter Router, List<string> Log) Setup()
    {
        var log = new List<string>();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);
        var bar = new MenuBar();

        bar.AddMenu("File", [new MenuItem("Open", () => log.Add("open")), new MenuItem("Exit", () => log.Add("exit"))]);
        bar.AddMenu("Help", [new MenuItem("About", () => log.Add("about"))]);

        root.Children.Add(bar);

        root.Overlays = layer;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        return (root, bar, layer, new PointerRouter(root, layer), log);
    }

    static MenuBarItem Title(MenuBar bar, int index) => (MenuBarItem)bar.Children[index];

    static (double X, double Y) Center(Control control)
    {
        var (x, y) = control.ToLocal(0, 0);

        return (-x + (control.Bounds.Width / 2), -y + (control.Bounds.Height / 2));
    }

    static void Click(PointerRouter router, Control control)
    {
        var (x, y) = Center(control);

        router.Down(x, y);
        router.Up(x, y);
    }

    static void Layout(OverlayLayer layer) => layer.Arrange(new Size(400, 300));

    [Fact]
    public void Clicking_a_title_opens_its_menu_below_it()
    {
        var (_, bar, layer, router, _) = Setup();
        var file = Title(bar, 0);

        Click(router, file);
        Layout(layer);

        Assert.True(bar.IsOpen);
        Assert.Same(file, bar.OpenItem);
        Assert.True(file.Menu.IsOpen);
        Assert.Contains("open", file.PseudoStates);
        Assert.Equal(file.Bounds.X, file.Menu.Bounds.X);
        Assert.Equal(file.Bounds.Bottom, file.Menu.Bounds.Y);
    }

    [Fact]
    public void Clicking_the_open_title_again_closes_its_menu()
    {
        var (_, bar, _, router, _) = Setup();
        var file = Title(bar, 0);

        Click(router, file);
        Click(router, file);

        Assert.False(bar.IsOpen);
        Assert.False(file.Menu.IsOpen);
        Assert.DoesNotContain("open", file.PseudoStates);
    }

    [Fact]
    public void Moving_over_another_title_while_a_menu_is_open_switches_to_its_menu()
    {
        var (_, bar, layer, router, _) = Setup();
        var (file, help) = (Title(bar, 0), Title(bar, 1));

        Click(router, file);
        Layout(layer);

        var (x, y) = Center(help);

        router.Move(x, y);
        Layout(layer);

        Assert.Same(help, bar.OpenItem);
        Assert.False(file.Menu.IsOpen);
        Assert.True(help.Menu.IsOpen);
        Assert.DoesNotContain("open", file.PseudoStates);
        Assert.Contains("open", help.PseudoStates);
    }

    [Fact]
    public void Moving_over_a_title_does_nothing_while_no_menu_is_open()
    {
        var (_, bar, _, router, _) = Setup();
        var (x, y) = Center(Title(bar, 1));

        router.Move(x, y);

        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void Clicking_the_title_that_the_pointer_just_switched_to_closes_its_menu()
    {
        var (_, bar, layer, router, _) = Setup();

        Click(router, Title(bar, 0));
        Layout(layer);
        Click(router, Title(bar, 1));

        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void A_press_outside_the_menus_closes_the_open_menu()
    {
        var (_, bar, layer, router, _) = Setup();

        Click(router, Title(bar, 0));
        Layout(layer);

        router.Down(300, 250);
        router.Up(300, 250);

        Assert.False(bar.IsOpen);
        Assert.DoesNotContain("open", Title(bar, 0).PseudoStates);
    }

    [Fact]
    public void Escape_closes_the_open_menu()
    {
        var (_, bar, layer, router, _) = Setup();

        Click(router, Title(bar, 0));

        Assert.True(layer.CloseTop());
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void Pressing_an_entry_runs_it_and_closes_the_menu()
    {
        var (_, bar, layer, router, log) = Setup();
        var file = Title(bar, 0);

        Click(router, file);
        Layout(layer);

        var (x, y) = (file.Menu.Bounds.X + 5, file.Menu.Bounds.Y + 25);

        router.Move(x, y);
        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(["exit"], log);
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void A_title_without_entries_does_not_open()
    {
        var (_, bar, _, router, _) = Setup();
        var empty = bar.AddMenu("Empty", []);

        Assert.False(bar.Open(empty));
        Assert.False(bar.IsOpen);
        Assert.DoesNotContain("open", empty.PseudoStates);
    }

    [Fact]
    public void A_title_shows_its_text_without_the_ampersand_and_remembers_the_access_key()
    {
        var bar = new MenuBar();
        var title = bar.AddMenu("&File", [new MenuItem("Open")]);

        Assert.Equal("File", title.Text);
        Assert.Equal('F', title.AccessKey);
    }

    [Fact]
    public void Alt_with_the_access_key_opens_the_menu_with_its_first_entry_highlighted()
    {
        var (root, bar, layer, _, _) = Setup();
        var title = bar.AddMenu("E&xtras", [new MenuItem("One"), new MenuItem("Two")]);

        Assert.True(MenuBar.AccessKeyDown(root, Key.X, KeyModifiers.Alt));
        Layout(layer);

        Assert.Same(title, bar.OpenItem);
        Assert.Equal(0, title.Menu.SelectedIndex);
    }

    [Fact]
    public void Alt_with_the_access_key_of_the_open_title_closes_its_menu()
    {
        var (root, bar, _, _, _) = Setup();

        bar.AddMenu("E&xtras", [new MenuItem("One")]);

        MenuBar.AccessKeyDown(root, Key.X, KeyModifiers.Alt);

        Assert.True(MenuBar.AccessKeyDown(root, Key.X, KeyModifiers.Alt));
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void A_letter_without_Alt_or_without_a_matching_title_is_not_an_access_key()
    {
        var (root, bar, _, _, _) = Setup();

        bar.AddMenu("E&xtras", [new MenuItem("One")]);

        Assert.False(MenuBar.AccessKeyDown(root, Key.X, KeyModifiers.None));
        Assert.False(MenuBar.AccessKeyDown(root, Key.Q, KeyModifiers.Alt));
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void Right_and_Left_move_to_the_menu_of_the_next_and_previous_title_and_wrap_around()
    {
        var (_, bar, layer, router, _) = Setup();
        var (file, help) = (Title(bar, 0), Title(bar, 1));

        Click(router, file);

        layer.KeyDown(Key.Right, KeyModifiers.None);

        Assert.Same(help, bar.OpenItem);
        Assert.Equal(0, help.Menu.SelectedIndex);

        layer.KeyDown(Key.Right, KeyModifiers.None);

        Assert.Same(file, bar.OpenItem);

        layer.KeyDown(Key.Left, KeyModifiers.None);

        Assert.Same(help, bar.OpenItem);
    }

    [Fact]
    public void Arrow_keys_move_the_highlight_in_the_open_menu_and_Enter_runs_the_entry()
    {
        var (_, bar, layer, router, log) = Setup();

        Click(router, Title(bar, 0));

        layer.KeyDown(Key.Down, KeyModifiers.None);
        layer.KeyDown(Key.Down, KeyModifiers.None);
        layer.KeyDown(Key.Enter, KeyModifiers.None);

        Assert.Equal(["exit"], log);
        Assert.False(bar.IsOpen);
    }

    [Fact]
    public void A_submenu_of_a_menu_bar_entry_closes_with_its_menu_when_another_title_opens()
    {
        var (root, bar, layer, router, _) = Setup();
        var more = bar.AddMenu("&More", [new MenuItem("Sub") { Children = [new MenuItem("Leaf")] }]);

        MenuBar.AccessKeyDown(root, Key.M, KeyModifiers.Alt);

        more.Menu.KeyDown(Key.Right);

        Assert.Equal(2, layer.Count);

        bar.Open(Title(bar, 0));

        Assert.Equal(1, layer.Count);
    }
}
