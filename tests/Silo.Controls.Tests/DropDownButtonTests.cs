using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class DropDownButtonTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "width": "400px", "height": "300px", "align-items": "flex-start" },
            "Button": { "padding": "4px 10px", "text-align": "center" },
            "SplitButton": { "flex-direction": "row", "gap": "2px" },
            "ContextMenu": { "item-height": "20px" }
          }
        }
        """;

    static (Box Root, OverlayLayer Layer, PointerRouter Router) Setup(params Control[] controls)
    {
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet.Replace("\"Box\"", "\"#root\""), "t.jsonc")]);

        foreach (var control in controls)
            root.Children.Add(control);

        root.Overlays = layer;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        return (root, layer, new PointerRouter(root, layer));
    }

    static void Press(PointerRouter router, Control control)
    {
        var (x, y) = control.ToLocal(0, 0);
        var (px, py) = (-x + (control.Bounds.Width / 2), -y + (control.Bounds.Height / 2));

        router.Move(px, py);
        router.Down(px, py);
        router.Up(px, py);
    }

    [Fact]
    public void A_DropDownButton_is_styled_as_a_Button_and_has_room_for_the_arrow()
    {
        var button = new DropDownButton { Text = "Export" };
        var plain = new Button { Text = "Export" };
        var (_, _, _) = Setup(button, plain);

        Assert.Equal("Button", button.TypeName);
        Assert.Contains("dropdown", button.Classes);
        Assert.Equal(plain.Bounds.Width + 6 + 12, button.Bounds.Width);
    }

    [Fact]
    public void Pressing_it_opens_the_menu_below_the_button_and_picking_an_entry_runs_it()
    {
        var picked = new List<string>();
        var button = new DropDownButton { Text = "Export", Entries = [new MenuItem("PDF", () => picked.Add("pdf")), new MenuItem("CSV", () => picked.Add("csv"))] };
        var (_, layer, router) = Setup(button);

        Press(router, button);
        layer.Arrange(new Size(400, 300));

        Assert.True(button.IsOpen);

        var (x, y) = button.Menu.ToLocal(0, 0);

        Assert.Equal(0, -x, 3);
        Assert.Equal(button.Bounds.Height, -y, 3);

        router.Move(5, button.Bounds.Height + 25);
        router.Down(5, button.Bounds.Height + 25);
        router.Up(5, button.Bounds.Height + 25);

        Assert.Equal(["csv"], picked);
        Assert.False(button.IsOpen);
    }

    [Fact]
    public void A_button_without_entries_does_not_open_and_a_second_press_closes_the_menu()
    {
        var empty = new DropDownButton { Text = "None" };
        var full = new DropDownButton { Text = "Some", Entries = [new MenuItem("A")] };
        var (_, layer, router) = Setup(empty, full);

        Press(router, empty);

        Assert.False(empty.IsOpen);

        Press(router, full);
        layer.Arrange(new Size(400, 300));

        Assert.True(full.IsOpen);

        Press(router, full);

        Assert.False(full.IsOpen);
    }

    [Fact]
    public void The_main_part_of_a_SplitButton_raises_Click_and_the_arrow_opens_the_menu()
    {
        var split = new SplitButton { Text = "Save", Entries = [new MenuItem("Save As")] };
        var clicks = 0;

        split.Click += (_, _) => clicks++;

        var (_, layer, router) = Setup(split);

        Press(router, split.Main);

        Assert.Equal(1, clicks);
        Assert.False(split.Arrow.IsOpen);

        Press(router, split.Arrow);
        layer.Arrange(new Size(400, 300));

        Assert.Equal(1, clicks);
        Assert.True(split.Arrow.IsOpen);
    }

    [Fact]
    public void The_menu_of_a_SplitButton_opens_below_the_whole_control()
    {
        var split = new SplitButton { Text = "Save", Entries = [new MenuItem("Save As")] };
        var (_, layer, router) = Setup(split);

        Press(router, split.Arrow);
        layer.Arrange(new Size(400, 300));

        var (x, y) = split.Menu.ToLocal(0, 0);
        var (sx, sy) = split.ToLocal(0, 0);

        Assert.Equal(-sx, -x, 3);
        Assert.Equal(-sy + split.Bounds.Height, -y, 3);
    }
}
