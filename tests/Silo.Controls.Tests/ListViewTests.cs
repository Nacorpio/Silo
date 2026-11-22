using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class ListViewTests
{
    static (Box Root, ListView List, PointerRouter Router) Setup(int count = 20)
    {
        var list = new ListView { Items = Enumerable.Range(0, count).Select(n => $"Row {n}").ToList() };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "padding": "10px", "align-items": "start" }, "ListView": { "width": "100px", "height": "100px", "item-height": "20px" } } }""";

        root.Children.Add(list);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 300));

        return (root, list, new PointerRouter(root));
    }

    [Fact]
    public void Clicking_a_row_selects_it_and_raises_the_event()
    {
        var (_, list, router) = Setup();
        var raised = 0;

        list.SelectionChanged += (_, _) => raised++;

        Assert.True(router.Down(20, 10 + 45));
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal(1, raised);

        Assert.False(router.Down(20, 10 + 50));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Scrolling_moves_the_rows_and_clicks_hit_the_row_under_the_pointer()
    {
        var (_, list, router) = Setup();

        Assert.Equal(300, list.MaxOffset);
        Assert.True(router.Scroll(20, 20, 60));
        Assert.Equal(60, list.Offset);

        router.Down(20, 10 + 5);

        Assert.Equal(3, list.SelectedIndex);
        Assert.True(router.Scroll(20, 20, 1000));
        Assert.Equal(300, list.Offset);
        Assert.False(router.Scroll(20, 20, 10));
    }

    [Fact]
    public void The_arrows_home_and_end_move_the_selection_and_keep_it_in_view()
    {
        var (_, list, _) = Setup();

        Assert.True(list.KeyDown(Key.Down));
        Assert.Equal(0, list.SelectedIndex);

        Assert.True(list.KeyDown(Key.End));
        Assert.Equal(19, list.SelectedIndex);
        Assert.Equal(300, list.Offset);

        Assert.False(list.KeyDown(Key.Down));
        Assert.True(list.KeyDown(Key.Up));
        Assert.Equal(18, list.SelectedIndex);

        Assert.True(list.KeyDown(Key.Home));
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal(0, list.Offset);
    }

    [Fact]
    public void Only_the_visible_rows_are_painted_and_the_selected_one_has_its_own_colors()
    {
        var (root, list, _) = Setup(10_000);
        var color = new Color(4, 5, 6);

        list.SetValue(ListView.SelectionColorProperty, color);
        list.SelectedIndex = 1;
        list.ScrollBy(2000);

        var texts = Commands(root).OfType<DrawTextCommand>().Select(c => c.Text).ToList();

        Assert.Equal(["Row 100", "Row 101", "Row 102", "Row 103", "Row 104", "Row 105"], texts);
        Assert.DoesNotContain(Commands(root), c => c is FillRectCommand f && f.Color == color);

        list.ScrollBy(-2000);

        Assert.Contains(Commands(root), c => c is FillRectCommand f && f.Color == color);
    }

    [Fact]
    public void Setting_new_items_clears_the_selection_and_selecting_outside_the_list_selects_nothing()
    {
        var (_, list, _) = Setup();

        list.SelectedIndex = 3;
        list.SelectedIndex = 99;

        Assert.Equal(-1, list.SelectedIndex);

        list.SelectedIndex = 3;
        list.Items = ["a", "b"];

        Assert.Equal(-1, list.SelectedIndex);
        Assert.Equal(0, list.Offset);
    }

    /// <summary>Renders the tree and returns the commands.</summary>
    static IReadOnlyList<DrawCommand> Commands(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands;
    }

    [Fact]
    public void Hover_selects_the_row_under_the_pointer_only_when_asked_to_and_never_scrolls()
    {
        var (_, list, router) = Setup();

        router.Move(20, 10 + 45);

        Assert.Equal(-1, list.SelectedIndex);

        list.HoverSelects = true;

        Assert.True(router.Move(20, 10 + 46));
        Assert.Equal(2, list.SelectedIndex);
        Assert.False(router.Move(20, 10 + 50));

        router.Move(20, 10 + 99);

        Assert.Equal(4, list.SelectedIndex);
        Assert.Equal(0, list.Offset);
    }

    [Fact]
    public void Pressing_a_row_or_Enter_raises_Activated_even_for_the_selected_row()
    {
        var (_, list, router) = Setup();
        var activated = new List<int>();

        list.Activated += (_, index) => activated.Add(index);

        router.Down(20, 10 + 45);
        router.Down(20, 10 + 45);
        list.KeyDown(Key.Enter);

        Assert.Equal([2, 2, 2], activated);
    }

    [Fact]
    public void Icons_are_drawn_in_front_of_the_text_and_every_row_lines_up()
    {
        var (root, list, _) = Setup(3);
        var icon = new FakeImage(32, 32);
        var builder = new DisplayListBuilder();

        list.Icons = [icon, null, icon];
        root.Arrange(new Size(300, 300));
        root.Render(builder);

        var commands = builder.Build().Commands;
        var images = commands.OfType<DrawImageCommand>().ToList();
        var texts = commands.OfType<DrawTextCommand>().ToList();

        Assert.Equal(2, images.Count);
        Assert.Equal(new Rect(8, 2, 16, 16), images[0].Destination);
        Assert.Equal(8 + 16 + 8, texts[1].X);
        Assert.Equal(texts[0].X, texts[2].X);
    }

    [Fact]
    public void A_mask_icon_is_drawn_in_the_text_color_of_its_row_and_turns_white_on_the_selected_row()
    {
        var (root, list, _) = Setup(2);
        var mask = new FakeMask(16, 16);
        var builder = new DisplayListBuilder();

        list.Icons = [mask, mask];
        list.SelectedIndex = 1;
        root.Arrange(new Size(300, 300));
        root.Render(builder);

        var masks = builder.Build().Commands.OfType<DrawMaskCommand>().ToList();

        Assert.Equal(2, masks.Count);
        Assert.Equal(list.GetValue(Control.ColorProperty), masks[0].Color);
        Assert.Equal(list.GetValue(ListView.SelectionTextColorProperty), masks[1].Color);
    }

    [Fact]
    public void Without_icons_the_text_starts_where_it_always_did()
    {
        var (root, list, _) = Setup(2);
        var builder = new DisplayListBuilder();

        list.Icons = [null, null];
        root.Render(builder);

        Assert.Equal(8, builder.Build().Commands.OfType<DrawTextCommand>().First().X);
    }
}
