using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class GalleryViewTests
{
    static (Box Root, GalleryView Gallery) Setup(int count = 20, GalleryLayout? layout = null, int width = 300, int height = 200)
    {
        var gallery = new GalleryView { Name = "g", Layout = layout ?? GalleryLayout.Grid };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        gallery.Items = Enumerable.Range(0, count).Select(i => (object)new GalleryItem(new FakeImage(100 + (i % 3) * 50, 100), $"Item {i}")).ToList();
        root.Children.Add(gallery);

        Arrange(root, gallery, width, height);

        return (root, gallery);
    }

    static void Arrange(Box root, GalleryView gallery, int width, int height)
    {
        var sheet = $$"""
            { "rules": { "Box": { "align-items": "start" }, "#g": { "width": "{{width}}px", "height": "{{height}}px",
              "item-width": "90px", "item-height": "80px", "gallery-gap": "10px", "gallery-padding": "0px" } } }
            """;

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(width + 50, height + 50));
    }

    [Fact]
    public void The_grid_wraps_cells_of_equal_size()
    {
        var (_, gallery) = Setup();

        Assert.Equal(new Rect(0, 0, 90, 80), gallery.ItemBounds(0));
        Assert.Equal(new Rect(100, 0, 90, 80), gallery.ItemBounds(1));
        Assert.Equal(new Rect(0, 90, 90, 80), gallery.ItemBounds(3));
    }

    [Fact]
    public void Fixed_columns_share_the_width()
    {
        var (root, gallery) = Setup();

        gallery.Columns = 2;
        Arrange(root, gallery, 300, 200);

        Assert.Equal(145, gallery.ItemBounds(0).Width);
        Assert.Equal(155, gallery.ItemBounds(1).X);
    }

    [Fact]
    public void Only_items_near_the_viewport_have_controls_and_scrolling_creates_the_rest()
    {
        var (root, gallery) = Setup(count: 300);

        Assert.InRange(gallery.RealizedCount, 6, 40);
        Assert.Equal(gallery.RealizedCount, gallery.Children.Count);

        gallery.ScrollBy(2000);
        Arrange(root, gallery, 300, 200);

        Assert.InRange(gallery.RealizedCount, 6, 40);
        Assert.Equal(2000, gallery.Offset);
    }

    [Fact]
    public void Justified_rows_fill_the_width_and_keep_the_aspect_ratio()
    {
        var (_, gallery) = Setup(layout: GalleryLayout.Justified);
        var first = gallery.ItemBounds(0);

        Assert.Equal(first.Y, gallery.ItemBounds(1).Y);

        var row = Enumerable.Range(0, 20).Where(i => gallery.ItemBounds(i).Y == first.Y).Select(gallery.ItemBounds).ToList();

        Assert.Equal(300, row.Max(r => r.Right), 3);
        Assert.Equal(1.0, first.Width / first.Height, 3);
    }

    [Fact]
    public void Masonry_stacks_each_item_under_the_shortest_column()
    {
        var (_, gallery) = Setup(layout: GalleryLayout.Masonry);

        Assert.Equal(0, gallery.ItemBounds(0).Y);
        Assert.Equal(0, gallery.ItemBounds(2).Y);
        Assert.True(gallery.ItemBounds(3).Y > 0);
        Assert.Equal(90, gallery.ItemBounds(3).Width);
    }

    [Fact]
    public void The_strip_is_one_row_that_scrolls_sideways()
    {
        var (_, gallery) = Setup(layout: GalleryLayout.Strip);

        Assert.All(Enumerable.Range(0, 20), i => Assert.Equal(0, gallery.ItemBounds(i).Y));
        Assert.True(gallery.MaxOffset > 0);
        Assert.True(gallery.ScrollBy(50));
    }

    [Fact]
    public void Pressing_selects_and_modifiers_toggle_and_extend()
    {
        var (_, gallery) = Setup();

        gallery.SelectionMode = GallerySelectionMode.Multiple;

        Assert.True(gallery.PointerDown(5, 5, KeyModifiers.None));
        Assert.Equal([0], gallery.SelectedIndices);

        gallery.PointerDown(105, 5, KeyModifiers.Control);
        Assert.Equal([0, 1], gallery.SelectedIndices);

        gallery.PointerDown(5, 95, KeyModifiers.Shift);
        Assert.Equal([1, 2, 3], gallery.SelectedIndices);
    }

    [Fact]
    public void Single_mode_keeps_one_item_and_none_ignores_presses()
    {
        var (_, gallery) = Setup();

        gallery.PointerDown(5, 5, KeyModifiers.None);
        gallery.PointerDown(105, 5, KeyModifiers.Control);

        Assert.Equal([1], gallery.SelectedIndices);

        gallery.SelectionMode = GallerySelectionMode.None;

        Assert.Empty(gallery.SelectedIndices);
    }

    [Fact]
    public void Arrow_keys_move_in_two_dimensions_and_scroll_the_item_into_view()
    {
        var (_, gallery) = Setup(count: 60);

        gallery.SelectedIndex = 0;

        gallery.KeyDown(Key.Right);
        Assert.Equal(1, gallery.SelectedIndex);

        gallery.KeyDown(Key.Down);
        Assert.Equal(4, gallery.SelectedIndex);

        gallery.KeyDown(Key.Up);
        Assert.Equal(1, gallery.SelectedIndex);

        gallery.KeyDown(Key.End);
        Assert.Equal(59, gallery.SelectedIndex);
        Assert.True(gallery.Offset > 0);
    }

    [Fact]
    public void Enter_and_a_quick_second_press_activate_the_item()
    {
        var (_, gallery) = Setup();
        var activated = new List<int>();
        var now = 1000L;

        gallery.Clock = () => now;
        gallery.ItemActivated += (_, index) => activated.Add(index);

        gallery.PointerDown(105, 5, KeyModifiers.None);
        now += 100;
        gallery.PointerDown(105, 5, KeyModifiers.None);
        gallery.KeyDown(Key.Enter);

        Assert.Equal([1, 1], activated);
    }

    [Fact]
    public void A_template_makes_the_item_controls_and_selected_cells_get_the_state()
    {
        var gallery = new GalleryView { Name = "g", ItemTemplate = item => new TextBlock { Text = $"#{item}" } };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        gallery.Items = new object[] { 1, 2, 3 };
        root.Children.Add(gallery);
        Arrange(root, gallery, 300, 200);

        gallery.SelectedIndex = 1;

        var cell = (Control)gallery.Children[1];

        Assert.Equal("#2", ((TextBlock)cell.Children[0]).Text);
        Assert.Contains("selected", cell.PseudoStates);
    }
}
