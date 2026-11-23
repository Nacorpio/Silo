using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ContextMenuExtrasTests
{
    const string Sheet = """{ "rules": { "Box": { "width": "300px", "height": "200px" }, "ContextMenu": { "item-height": "20px" } } }""";

    static (ContextMenu Menu, OverlayLayer Layer, PointerRouter Router) Open(params MenuItem[] entries)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);
        var menu = new ContextMenu { Entries = entries };

        root.Overlays = layer;
        root.ContextMenu = menu;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(300, 200));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        var router = new PointerRouter(root, layer);

        router.SecondaryDown(0, 0);
        layer.Arrange(new Size(300, 200));

        return (menu, layer, router);
    }

    static void Pick(PointerRouter router, int row)
    {
        router.Move(5, (row * 20) + 5);
        router.Down(5, (row * 20) + 5);
        router.Up(5, (row * 20) + 5);
    }

    [Fact]
    public void Picking_a_check_item_flips_it_and_runs_its_click()
    {
        var runs = 0;
        var item = new MenuItem("Wrap", () => runs++) { IsChecked = false };
        var (menu, _, router) = Open(item);

        Pick(router, 0);

        Assert.True(item.IsChecked);
        Assert.Equal(1, runs);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Picking_a_radio_item_turns_the_others_of_its_group_off()
    {
        var small = new MenuItem("Small") { IsChecked = true, GroupName = "size" };
        var large = new MenuItem("Large") { IsChecked = false, GroupName = "size" };
        var other = new MenuItem("Other") { IsChecked = true, GroupName = "mode" };
        var (_, _, router) = Open(small, large, other);

        Pick(router, 1);

        Assert.False(small.IsChecked);
        Assert.True(large.IsChecked);
        Assert.True(other.IsChecked);
    }

    [Fact]
    public void A_disabled_item_cannot_be_picked_or_selected()
    {
        var runs = 0;
        var (menu, _, router) = Open(new MenuItem("Paste", () => runs++) { IsEnabled = false }, new MenuItem("Copy"));

        Pick(router, 0);

        Assert.Equal(0, runs);
        Assert.True(menu.IsOpen);
        Assert.Equal(-1, menu.SelectedIndex);

        menu.KeyDown(Key.Down);

        Assert.Equal(1, menu.SelectedIndex);
    }

    [Fact]
    public void A_disabled_item_ignores_its_access_key()
    {
        var runs = 0;
        var (menu, _, _) = Open(new MenuItem("&Paste", () => runs++) { IsEnabled = false });

        menu.KeyDown(Key.P);

        Assert.Equal(0, runs);
    }

    [Fact]
    public void Check_marks_and_shortcuts_make_the_menu_wider()
    {
        var (plain, plainLayer, _) = Open(new MenuItem("Copy"));
        var (rich, richLayer, _) = Open(new MenuItem("Copy") { ShortcutText = "Ctrl+C", IsChecked = true });

        Assert.Equal(plain.Bounds.Width + 22 + 6 * 7 + 24, rich.Bounds.Width);
    }
}

public class ContextMenuGlyphTests
{
    [Fact]
    public void A_glyph_takes_the_room_of_an_icon_and_shifts_the_text()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var plain = new ContextMenu { Entries = [new MenuItem("Copy")] };
        var withGlyph = new ContextMenu { Entries = [new MenuItem("Copy") { Glyph = "plus" }] };

        foreach (var menu in new[] { plain, withGlyph })
        {
            root.Children.Add(menu);
        }

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "Box": { "align-items": "start" } } }""", "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        Assert.True(withGlyph.Bounds.Width > plain.Bounds.Width);
    }
}
