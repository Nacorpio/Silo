using Silo;
using Silo.Studio;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class StudioViewTests
{
    static StyleResolver Resolver(ThemeVariant variant) => new([.. DefaultTheme.Load(variant), StudioStyles.Load()]);

    [Fact]
    public void The_view_starts_in_the_light_variant_and_is_a_Window_for_the_theme()
    {
        var view = new StudioView();

        Assert.Equal(ThemeVariant.Light, view.Variant);
        Assert.Equal("Window", view.TypeName);
        Assert.Equal("Silo Studio", view.Title);
    }

    [Fact]
    public void Picking_a_variant_raises_VariantChanged_once_and_a_repeat_does_nothing()
    {
        var view = new StudioView();
        var raised = 0;

        view.VariantChanged += (_, _) => raised++;

        view.Variant = ThemeVariant.Dark;
        view.Variant = ThemeVariant.Dark;

        Assert.Equal(1, raised);
    }

    [Fact]
    public void The_buttons_pick_the_variant_and_the_picked_one_has_the_selected_Class()
    {
        var view = new StudioView();
        var dark = FindButton(view, "dark");

        dark.PointerDown();
        dark.PointerUp(inside: true);

        Assert.Equal(ThemeVariant.Dark, view.Variant);
        Assert.Contains("selected", dark.Classes);
        Assert.DoesNotContain("selected", FindButton(view, "light").Classes);
    }

    [Fact]
    public void The_dark_variant_gives_the_window_the_dark_surface()
    {
        var view = new StudioView();

        view.ApplyStyles(Resolver(ThemeVariant.Light));

        Assert.Equal(new Color(255, 255, 255), view.GetValue(Control.BackgroundProperty));

        view.ApplyStyles(Resolver(ThemeVariant.Dark));

        Assert.Equal(new Color(0x11, 0x18, 0x27), view.GetValue(Control.BackgroundProperty));
    }

    [Fact]
    public void The_title_bar_sits_above_the_matrix()
    {
        var view = new StudioView { TextMeasurer = new FixedMeasurer() };

        view.ApplyStyles(Resolver(ThemeVariant.Light));
        view.Arrange(new Silo.Layout.Size(view.InitialWidth, view.InitialHeight));

        var bar = (Control)view.Children[0];
        var scroll = (Control)view.Children[1];

        Assert.True(scroll.Bounds.Y >= bar.Bounds.Bottom);
    }

    static Button FindButton(Control root, string name)
        => (Button)(Find(root, name) ?? throw new InvalidOperationException(name));

    static Control? Find(Control root, string name)
    {
        if (root.Name == name)
            return root;

        foreach (var child in root.Children)
            if (Find((Control)child, name) is { } found)
                return found;

        return null;
    }
}
