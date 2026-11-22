using Silo;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class DefaultThemeTests
{
    static StyleResolver Resolver() => new([DefaultTheme.Load()]);

    [Fact]
    public void The_embedded_theme_parses_without_diagnostics()
    {
        var button = new Button { Text = "Ok" };

        var diagnostics = button.ApplyStyles(Resolver());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Buttons_use_the_accent_and_change_on_hover_and_press()
    {
        var button = new Button { Text = "Ok" };

        button.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), button.GetValue(Control.BackgroundProperty));

        button.SetHovered(true);
        button.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x25, 0x63, 0xeb), button.GetValue(Control.BackgroundProperty));

        button.SetPressed(true);
        button.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x1d, 0x4e, 0xd8), button.GetValue(Control.BackgroundProperty));
    }

    [Fact]
    public void Text_inherits_the_theme_color_and_font_size()
    {
        var root = new Box();
        var text = new TextBlock { Text = "Hi" };

        root.Children.Add(text);
        root.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x1f, 0x29, 0x37), text.GetValue(Control.ColorProperty));
        Assert.Equal(14, text.GetValue(Control.FontSizeProperty));
    }

    [Fact]
    public void Toggles_show_the_accent_when_checked_and_the_progress_bar_uses_the_track_tokens()
    {
        var check = new CheckBox { Text = "Ok" };
        var toggle = new ToggleSwitch { Text = "Ok" };
        var bar = new ProgressBar();

        check.IsChecked = true;
        toggle.IsChecked = true;

        Assert.Empty(check.ApplyStyles(Resolver()));
        Assert.Empty(toggle.ApplyStyles(Resolver()));
        Assert.Empty(bar.ApplyStyles(Resolver()));

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), check.GetValue(ToggleButton.IndicatorBackgroundProperty));
        Assert.Equal(new Color(0x3b, 0x82, 0xf6), toggle.GetValue(ToggleButton.IndicatorBackgroundProperty));
        Assert.Equal(new Color(0xe5, 0xe7, 0xeb), bar.GetValue(Control.BackgroundProperty));
    }

    [Fact]
    public void Sliders_use_the_accent_and_darken_while_dragged()
    {
        var slider = new Slider();

        slider.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), slider.GetValue(Slider.FillColorProperty));

        slider.PointerDown(0);

        Assert.Empty(slider.ApplyStyles(Resolver()));
        Assert.Equal(new Color(0x1d, 0x4e, 0xd8), slider.GetValue(Slider.FillColorProperty));
    }

    [Fact]
    public void Text_boxes_get_an_accent_border_while_focused()
    {
        var box = new TextBox();

        Assert.Empty(box.ApplyStyles(Resolver()));
        Assert.Equal(new Color(0x9c, 0xa3, 0xaf), box.GetValue(Control.BorderColorProperty));

        box.SetFocused(true);
        box.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), box.GetValue(Control.BorderColorProperty));
    }

    [Fact]
    public void List_views_select_with_the_accent_and_get_a_default_height()
    {
        var list = new ListView();

        Assert.Empty(list.ApplyStyles(Resolver()));
        Assert.Equal(new Color(0x3b, 0x82, 0xf6), list.GetValue(ListView.SelectionColorProperty));
        Assert.Equal(new Length(160, LengthUnit.Px), list.GetValue(Control.HeightProperty));
    }

    [Fact]
    public void Combo_boxes_look_like_text_boxes_and_get_an_accent_border_while_focused()
    {
        var combo = new ComboBox();

        Assert.Empty(combo.ApplyStyles(Resolver()));
        Assert.Equal(new Color(0x9c, 0xa3, 0xaf), combo.GetValue(Control.BorderColorProperty));

        combo.SetFocused(true);
        combo.ApplyStyles(Resolver());

        Assert.Equal(new Color(0x3b, 0x82, 0xf6), combo.GetValue(Control.BorderColorProperty));
    }

    [Fact]
    public void Context_menus_get_a_surface_a_border_and_the_accent_for_the_highlighted_row()
    {
        var menu = new ContextMenu();

        Assert.Empty(menu.ApplyStyles(Resolver()));
        Assert.Equal(new Color(0x9c, 0xa3, 0xaf), menu.GetValue(Control.BorderColorProperty));
        Assert.Equal(new Color(0x3b, 0x82, 0xf6), menu.GetValue(ListView.SelectionColorProperty));
    }

    [Fact]
    public void Text_is_set_in_Segoe_UI_by_default()
    {
        var text = new TextBlock();

        Assert.Empty(text.ApplyStyles(Resolver()));
        Assert.Equal("Segoe UI", text.GetValue(Control.FontFamilyProperty));
    }
}
