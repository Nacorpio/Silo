using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class ComboBoxTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "padding": "10px", "align-items": "start" },
            "ComboBox": { "width": "120px", "padding": "4px" },
            "ListView": { "border-width": "1px" }
          }
        }
        """;

    static (Box Root, ComboBox Combo, OverlayLayer Layer, PointerRouter Router, StyleResolver Resolver) Setup(
        int count = 4)
    {
        var combo = new ComboBox { Items = Enumerable.Range(0, count).Select(n => $"Item {n}").ToList() };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        root.Overlays = layer;
        root.Children.Add(combo);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        return (root, combo, layer, new PointerRouter(root, layer), resolver);
    }

    static void Layout(OverlayLayer layer, StyleResolver resolver)
    {
        layer.ApplyStyles(resolver);
        layer.Arrange(new Size(400, 300));
    }

    [Fact]
    public void Pressing_it_opens_a_list_right_below_with_its_width()
    {
        var (_, combo, layer, router, resolver) = Setup();

        Assert.True(router.Down(combo.Bounds.X + 5, combo.Bounds.Y + 5));
        Layout(layer, resolver);

        Assert.True(combo.IsOpen);
        Assert.Equal(1, layer.Count);

        var hit = layer.HitTest(combo.Bounds.X + 5, combo.Bounds.Bottom + 5);

        Assert.IsAssignableFrom<ListView>(hit);
        Assert.Equal(120, hit!.Bounds.Width);
        Assert.Equal(combo.Bounds.Bottom, hit.Bounds.Y);
        Assert.Equal(4 * 28 + 2, hit.Bounds.Height);
    }

    [Fact]
    public void Pressing_a_row_selects_it_and_closes_the_list()
    {
        var (_, combo, layer, router, resolver) = Setup();
        var raised = 0;

        combo.SelectionChanged += (_, _) => raised++;

        combo.Open();
        Layout(layer, resolver);

        var y = combo.Bounds.Bottom + 1 + 28 * 2 + 5;

        router.Down(combo.Bounds.X + 5, y);

        Assert.False(combo.IsOpen);
        Assert.Equal(2, combo.SelectedIndex);
        Assert.Equal("Item 2", combo.SelectedItem);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Pressing_the_selected_row_closes_the_list_without_a_change()
    {
        var (_, combo, layer, router, resolver) = Setup();

        combo.SelectedIndex = 0;
        combo.Open();
        Layout(layer, resolver);

        router.Down(combo.Bounds.X + 5, combo.Bounds.Bottom + 1 + 5);

        Assert.False(combo.IsOpen);
        Assert.Equal(0, combo.SelectedIndex);
    }

    [Fact]
    public void Pressing_outside_or_on_the_combo_again_closes_the_list_unchanged()
    {
        var (_, combo, layer, router, resolver) = Setup();

        combo.Open();
        Layout(layer, resolver);

        router.Down(combo.Bounds.X + 5, combo.Bounds.Y + 5);

        Assert.False(combo.IsOpen);
        Assert.Equal(-1, combo.SelectedIndex);

        combo.Open();
        Layout(layer, resolver);

        router.Down(390, 290);

        Assert.False(combo.IsOpen);
    }

    [Fact]
    public void The_list_scrolls_beyond_max_rows()
    {
        var (_, combo, layer, router, resolver) = Setup(20);

        combo.Open();
        Layout(layer, resolver);

        var hit = layer.HitTest(combo.Bounds.X + 5, combo.Bounds.Bottom + 5)!;

        Assert.Equal(8 * 28 + 2, hit.Bounds.Height);
        Assert.True(router.Scroll(combo.Bounds.X + 5, combo.Bounds.Bottom + 5, 56));
    }

    [Fact]
    public void Keys_open_the_list_move_through_it_and_pick_with_Enter()
    {
        var (_, combo, layer, _, resolver) = Setup();

        Assert.True(combo.KeyDown(Key.Enter));
        Layout(layer, resolver);
        Assert.True(combo.IsOpen);

        combo.KeyDown(Key.Down);
        combo.KeyDown(Key.Down);

        Assert.Equal(-1, combo.SelectedIndex);

        combo.KeyDown(Key.Enter);

        Assert.False(combo.IsOpen);
        Assert.Equal(1, combo.SelectedIndex);
    }

    [Fact]
    public void While_closed_Up_and_Down_change_the_selection_within_range()
    {
        var (_, combo, _, _, _) = Setup(3);

        combo.KeyDown(Key.Down);
        combo.KeyDown(Key.Down);
        combo.KeyDown(Key.Down);
        combo.KeyDown(Key.Down);

        Assert.Equal(2, combo.SelectedIndex);

        combo.KeyDown(Key.Up);

        Assert.Equal(1, combo.SelectedIndex);
    }

    [Fact]
    public void It_needs_choices_and_a_window_to_open()
    {
        var (_, combo, _, _, _) = Setup(0);
        var loose = new ComboBox { Items = ["A"] };

        Assert.False(combo.Open());
        Assert.False(loose.Open());
    }

    [Fact]
    public void Setting_new_items_clears_the_selection_and_closes_the_list()
    {
        var (_, combo, layer, _, _) = Setup();

        combo.SelectedIndex = 1;
        combo.Open();

        combo.Items = ["X"];

        Assert.False(combo.IsOpen);
        Assert.Equal(0, layer.Count);
        Assert.Equal(-1, combo.SelectedIndex);
    }
}
