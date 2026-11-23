using Silo;
using Silo.Layout;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class ColorPickerTests
{
    static (ColorPicker Picker, OverlayLayer Layer, PointerRouter Router) Setup()
    {
        var picker = new ColorPicker { Color = new Color(255, 0, 0) };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "ColorPicker": { "width": "140px" } } }""", "t.jsonc")]);

        root.Overlays = layer;
        root.Children.Add(picker);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        return (picker, layer, new PointerRouter(root, layer));
    }

    [Fact]
    public void Its_text_is_the_hex_code()
    {
        var picker = new ColorPicker { Color = new Color(1, 2, 3, 4) };

        Assert.Equal("#01020304", picker.Text);
    }

    [Fact]
    public void Pressing_it_opens_a_panel_whose_keys_change_the_color_live()
    {
        var (picker, layer, router) = Setup();
        var raised = 0;

        picker.ColorChanged += (_, _) => raised++;

        Assert.True(router.Down(picker.Bounds.X + 5, picker.Bounds.Y + 5));
        Assert.True(picker.IsOpen);
        Assert.Equal(1, layer.Count);

        Assert.True(layer.KeyDown(Key.Left, KeyModifiers.Shift));

        Assert.Equal(new Color(255, 25, 25), picker.Color);
        Assert.Equal(1, raised);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void Enter_on_a_surface_closes_the_panel_and_keeps_the_color()
    {
        var (picker, layer, _) = Setup();

        picker.KeyDown(Key.Enter);
        layer.KeyDown(Key.Left, KeyModifiers.Shift);
        layer.KeyDown(Key.Enter, KeyModifiers.None);

        Assert.False(picker.IsOpen);
        Assert.NotEqual(new Color(255, 0, 0), picker.Color);
    }

    [Fact]
    public void Typed_text_goes_to_the_hex_field_of_the_open_panel()
    {
        var (picker, layer, _) = Setup();

        picker.KeyDown(Key.Enter);

        Assert.False(picker.TextInput("x"));

        layer.KeyDown(Key.Tab, KeyModifiers.None);
        layer.KeyDown(Key.Tab, KeyModifiers.None);

        picker.KeyDown(Key.End);

        Assert.True(picker.TextInput("0"));
    }

    [Fact]
    public void Escape_closes_the_panel_without_changing_the_color()
    {
        var (picker, layer, _) = Setup();

        picker.KeyDown(Key.Enter);
        layer.CloseTop();

        Assert.False(picker.IsOpen);
        Assert.Equal(new Color(255, 0, 0), picker.Color);
    }
}
