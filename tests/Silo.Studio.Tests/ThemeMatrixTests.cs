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
    public void The_rows_are_grouped_into_one_tab_page_for_each_category()
    {
        var matrix = new ThemeMatrix();

        Assert.Equal(["Buttons", "Text input", "Values and lists", "Dates", "Layout", "Menus and dialogs"], ThemeMatrix.Categories);
        Assert.Equal(ThemeMatrix.Categories.Count(), matrix.Tabs.Count);
        Assert.Equal(0, matrix.Tabs.SelectedIndex);
        Assert.NotNull(matrix.Cell("QuantityBox", "focus"));
    }

    [Fact]
    public void Showing_a_category_selects_its_page_and_an_unknown_category_is_refused()
    {
        var matrix = new ThemeMatrix();

        Assert.True(matrix.ShowCategory("Text input"));
        Assert.False(matrix.ShowCategory("Text input"));
        Assert.Equal(1, matrix.Tabs.SelectedIndex);
        Assert.Equal("page-Text input", matrix.Tabs.SelectedContent!.Name);
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
}
