using System.Globalization;
using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class DatePickerTests
{
    static (Box Root, DatePicker Picker, OverlayLayer Layer, PointerRouter Router) Setup()
    {
        var picker = new DatePicker { Culture = CultureInfo.InvariantCulture };
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "DatePicker": { "width": "120px" } } }""", "t.jsonc")]);

        root.Overlays = layer;
        root.Children.Add(picker);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        return (root, picker, layer, new PointerRouter(root, layer));
    }

    [Fact]
    public void Its_text_is_the_date_in_the_format()
    {
        var picker = new DatePicker { Culture = CultureInfo.InvariantCulture };

        Assert.Equal(string.Empty, picker.Text);

        picker.SelectedDate = new DateOnly(2026, 11, 23);
        Assert.Equal("11/23/2026", picker.Text);

        picker.Format = "yyyy-MM-dd";
        Assert.Equal("2026-11-23", picker.Text);
    }

    [Fact]
    public void Pressing_it_opens_a_calendar_and_enter_picks_a_date_and_closes_it()
    {
        var (_, picker, layer, router) = Setup();
        var raised = 0;

        picker.SelectedDateChanged += (_, _) => raised++;

        Assert.True(router.Down(picker.Bounds.X + 5, picker.Bounds.Y + 5));
        Assert.True(picker.IsOpen);
        Assert.Equal(1, layer.Count);

        Assert.True(layer.KeyDown(Key.Right, KeyModifiers.None));
        Assert.True(layer.KeyDown(Key.Enter, KeyModifiers.None));

        Assert.False(picker.IsOpen);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddDays(1), picker.SelectedDate);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Escape_closes_the_calendar_without_picking()
    {
        var (_, picker, layer, _) = Setup();

        picker.KeyDown(Key.Enter);
        Assert.True(picker.IsOpen);

        layer.CloseTop();

        Assert.False(picker.IsOpen);
        Assert.Null(picker.SelectedDate);
    }

    [Fact]
    public void The_limits_are_passed_to_the_calendar()
    {
        var (_, picker, _, _) = Setup();

        picker.Minimum = new DateOnly(2026, 1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SelectedDate = new DateOnly(2025, 12, 31));
    }
}
