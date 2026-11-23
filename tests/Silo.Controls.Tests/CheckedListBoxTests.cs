using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class CheckedListBoxTests
{
    static (Box Root, CheckedListBox List, PointerRouter Router) Setup()
    {
        var list = new CheckedListBox { Items = ["A", "B", "C", "D"] };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """{ "rules": { "Box": { "padding": "10px", "align-items": "start" }, "CheckedListBox": { "width": "120px", "height": "100px", "item-height": "20px" } } }""";

        root.Children.Add(list);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 300));

        return (root, list, new PointerRouter(root));
    }

    [Fact]
    public void Pressing_a_row_flips_its_check()
    {
        var (_, list, router) = Setup();
        var raised = 0;

        list.CheckedChanged += (_, _) => raised++;

        router.Down(30, 10 + 25);
        router.Up(30, 10 + 25);

        Assert.Equal([1], list.CheckedIndices);

        router.Down(30, 10 + 25);
        router.Up(30, 10 + 25);

        Assert.Empty(list.CheckedIndices);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Space_and_enter_flip_the_selected_row_but_arrows_only_select()
    {
        var (_, list, _) = Setup();

        list.SelectedIndex = 0;
        list.KeyDown(Key.Down);

        Assert.Empty(list.CheckedIndices);

        Assert.True(list.TextInput(" "));
        Assert.Equal([1], list.CheckedIndices);

        list.KeyDown(Key.Enter);

        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void Space_without_a_selection_does_nothing()
    {
        var (_, list, _) = Setup();

        Assert.False(list.TextInput(" "));
        Assert.False(list.TextInput("x"));
    }

    [Fact]
    public void Select_all_and_none_and_setting_the_items_clear_or_fill_the_checks()
    {
        var (_, list, _) = Setup();

        list.SelectAll();

        Assert.Equal([0, 1, 2, 3], list.CheckedIndices);

        list.SelectAll(false);

        Assert.Empty(list.CheckedIndices);

        list.SetChecked(2, true);
        list.Items = ["X"];

        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void An_index_outside_the_list_cannot_be_checked()
    {
        var (_, list, _) = Setup();

        list.SetChecked(9, true);
        list.SetChecked(-1, true);

        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void A_checked_row_draws_a_tick_and_a_plain_row_only_an_outline()
    {
        var (root, list, _) = Setup();

        list.SetChecked(0, true);

        var builder = new DisplayListBuilder();

        root.Render(builder);

        var commands = builder.Build().Commands;

        Assert.Single(commands.OfType<StrokePolylineCommand>());
    }
}
