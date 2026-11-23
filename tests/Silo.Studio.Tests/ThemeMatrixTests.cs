using Silo;
using Silo.Studio;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class ThemeMatrixTests
{
    static StyleResolver Resolver(ThemeVariant variant) => new([.. DefaultTheme.Load(variant), StudioStyles.Load()]);

    [Fact]
    public void Every_state_that_applies_to_a_row_has_a_Control_and_the_others_have_none()
    {
        var matrix = new ThemeMatrix();

        Assert.NotNull(matrix.Cell("Button", "hover"));
        Assert.Null(matrix.Cell("Button", "checked"));
        Assert.NotNull(matrix.Cell("CheckBox", "checked"));
        Assert.Null(matrix.Cell("ProgressBar", "hover"));
        Assert.Null(matrix.Cell("Missing", "normal"));
        Assert.NotNull(matrix.Cell("Separator", "normal"));
        Assert.True(((Expander)matrix.Cell("Expander", "normal")!).IsExpanded);
    }

    [Fact]
    public void A_Control_in_a_column_has_that_Pseudo_state_and_the_checked_column_is_checked()
    {
        var matrix = new ThemeMatrix();

        Assert.Contains("hover", matrix.Cell("Button", "hover")!.PseudoStates);
        Assert.Contains("active", matrix.Cell("Button", "pressed")!.PseudoStates);
        Assert.Contains("focus", matrix.Cell("TextBox", "focus")!.PseudoStates);
        Assert.DoesNotContain("hover", matrix.Cell("Button", "normal")!.PseudoStates);
        Assert.True(((CheckBox)matrix.Cell("CheckBox", "checked")!).IsChecked);
        Assert.False(((CheckBox)matrix.Cell("CheckBox", "normal")!).IsChecked);
    }

    [Fact]
    public void The_theme_Rules_for_a_state_apply_to_its_cell()
    {
        var matrix = new ThemeMatrix();

        Assert.Empty(matrix.ApplyStyles(Resolver(ThemeVariant.Light)));

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), matrix.Cell("Button", "normal")!.GetValue(Control.BackgroundProperty));
        Assert.Equal(new Color(0x25, 0x63, 0xeb), matrix.Cell("Button", "hover")!.GetValue(Control.BackgroundProperty));
        Assert.Equal(new Color(0x1d, 0x4e, 0xd8), matrix.Cell("Button", "pressed")!.GetValue(Control.BackgroundProperty));
    }

    [Fact]
    public void The_matrix_is_styled_without_diagnostics_in_both_variants()
    {
        var matrix = new ThemeMatrix();

        Assert.Empty(matrix.ApplyStyles(Resolver(ThemeVariant.Light)));
        Assert.Empty(matrix.ApplyStyles(Resolver(ThemeVariant.Dark)));
    }

    [Fact]
    public void The_rows_are_grouped_into_one_page_for_each_category()
    {
        var matrix = new ThemeMatrix();

        Assert.Equal(["Buttons", "Text input", "Values and lists", "Inputs", "Data", "Dates", "Colors", "Display", "Panels", "Navigation", "Shell", "Collections", "Layout", "Menus and dialogs"], ThemeMatrix.Categories);
        Assert.Equal("Buttons", matrix.SelectedCategory);
        Assert.Single(matrix.Children);
        Assert.NotNull(matrix.Cell("QuantityBox", "focus"));
    }

    [Fact]
    public void Showing_a_category_selects_its_page_and_an_unknown_category_is_refused()
    {
        var matrix = new ThemeMatrix();

        Assert.True(matrix.ShowCategory("Text input"));
        Assert.False(matrix.ShowCategory("Text input"));
        Assert.Equal("Text input", matrix.SelectedCategory);
        Assert.Equal("page-Text input", matrix.Page.Name);
        Assert.Same(matrix.Page, Assert.Single(matrix.Children));
        Assert.Throws<ArgumentException>(() => matrix.ShowCategory("Nope"));
    }

    [Fact]
    public void Every_page_is_styled_without_diagnostics()
    {
        var matrix = new ThemeMatrix();

        foreach (var category in ThemeMatrix.Categories)
        {
            matrix.ShowCategory(category);

            Assert.Empty(matrix.ApplyStyles(Resolver(ThemeVariant.Light)));
        }
    }

    [Fact]
    public void Every_category_has_a_glyph_a_description_and_a_count_that_adds_up_to_the_rows()
    {
        var infos = ThemeMatrix.Categories.Select(ThemeMatrix.Describe).ToList();

        Assert.All(infos, i => Assert.True(Silo.Glyphs.TryGet(i.Glyph, out _)));
        Assert.All(infos, i => Assert.NotEmpty(i.Description));
        Assert.Equal(ThemeMatrix.RowNames.Count(), infos.Sum(i => i.Count));
    }

    [Fact]
    public void The_category_list_shows_the_selected_category_and_picks_one_on_a_press_or_a_key()
    {
        var matrix = new ThemeMatrix();
        var list = new CategoryList(matrix);

        Assert.Contains("selected", list.Entry("Buttons").PseudoStates);

        list.Entry("Dates").PointerDown();
        list.Entry("Dates").PointerUp(inside: true);

        Assert.Equal("Dates", matrix.SelectedCategory);
        Assert.Contains("selected", list.Entry("Dates").PseudoStates);
        Assert.DoesNotContain("selected", list.Entry("Buttons").PseudoStates);

        Assert.True(list.KeyDown(Key.Down));
        Assert.Equal("Colors", matrix.SelectedCategory);
        Assert.True(list.KeyDown(Key.Home));
        Assert.Equal("Buttons", matrix.SelectedCategory);
        Assert.False(list.KeyDown(Key.Up));
    }

    [Fact]
    public void Dividers_between_the_entries_are_an_option()
    {
        var list = new CategoryList(new ThemeMatrix());
        var count = ThemeMatrix.Categories.Count();

        Assert.Equal(count, list.Items.Children.Count);

        list.ShowDividers = true;

        Assert.Equal((count * 2) - 1, list.Items.Children.Count);
        Assert.IsType<Separator>(list.Items.Children[1]);
        Assert.Contains("category-divider", ((Control)list.Items.Children[1]).Classes);

        list.ShowDividers = false;

        Assert.Equal(count, list.Items.Children.Count);
    }
}
