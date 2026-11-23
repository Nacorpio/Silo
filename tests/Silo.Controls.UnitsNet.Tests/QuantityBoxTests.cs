using Silo;
using Silo.Validation;
using UnitsNet;
using UnitsNet.Units;
using Xunit;

public class QuantityBoxTests
{
    static NumberBox Number(QuantityBox box) => (NumberBox)box.Children[0];

    static ComboBox Dropdown(QuantityBox box) => (ComboBox)box.Children[2];

    static QuantityBox CreateLength()
        => new() { QuantityType = "Length" };

    [Fact]
    public void A_quantity_type_offers_its_units_with_abbreviations_and_starts_at_zero_in_the_base_unit()
    {
        var box = new QuantityBox { Culture = System.Globalization.CultureInfo.InvariantCulture, QuantityType = "Length" };

        Assert.Equal("Length", box.QuantityType);
        Assert.Equal(LengthUnit.Meter, box.Unit);
        Assert.Equal(Length.Zero, box.Value);
        Assert.Contains("cm", Dropdown(box).Items);
        Assert.Equal("m", Dropdown(box).SelectedItem);
        Assert.Equal("0", Number(box).Text);
        Assert.Equal(box.Units.Count, Dropdown(box).Items.Count);
    }

    [Fact]
    public void An_unknown_quantity_type_is_refused_and_null_empties_the_box()
    {
        var box = CreateLength();

        Assert.Throws<ArgumentException>(() => box.QuantityType = "Nonsense");

        box.QuantityType = null;

        Assert.Null(box.Value);
        Assert.Empty(box.Units);
    }

    [Fact]
    public void Setting_a_value_selects_its_unit_and_shows_its_number()
    {
        var box = CreateLength();

        box.Value = Length.FromFeet(2);

        Assert.Equal(LengthUnit.Foot, box.Unit);
        Assert.Equal("2", Number(box).Text);
    }

    [Fact]
    public void A_value_of_another_kind_is_refused_but_an_empty_box_adopts_the_kind()
    {
        var box = CreateLength();

        Assert.Throws<ArgumentException>(() => box.Value = Mass.FromKilograms(1));

        var empty = new QuantityBox { Value = Mass.FromKilograms(3) };

        Assert.Equal("Mass", empty.QuantityType);
        Assert.Equal(MassUnit.Kilogram, empty.Unit);
    }

    [Fact]
    public void Picking_another_unit_converts_the_number_and_raises_both_events()
    {
        var box = CreateLength();
        var values = 0;
        var units = 0;

        box.Value = Length.FromMeters(1);
        box.ValueChanged += (_, _) => values++;
        box.UnitChanged += (_, _) => units++;

        var dropdown = Dropdown(box);

        dropdown.SelectedIndex = dropdown.Items.ToList().IndexOf("cm");

        Assert.Equal(LengthUnit.Centimeter, box.Unit);
        Assert.Equal(100, box.Value!.As(LengthUnit.Centimeter), 9);
        Assert.Equal("100", Number(box).Text);
        Assert.Equal(1, values);
        Assert.Equal(1, units);
    }

    [Fact]
    public void Without_conversion_the_digits_stay_and_the_quantity_changes()
    {
        var box = CreateLength();

        box.ConvertOnUnitChange = false;
        box.Value = Length.FromMeters(1);
        box.Unit = LengthUnit.Centimeter;

        Assert.Equal(1, box.Value!.As(LengthUnit.Centimeter), 9);
        Assert.Equal("1", Number(box).Text);
    }

    [Fact]
    public void Typing_and_stepping_the_number_change_the_value_in_the_selected_unit()
    {
        var box = CreateLength();

        box.Unit = LengthUnit.Foot;
        Number(box).TextInput("5");

        Assert.Equal(Length.FromFeet(5), box.Value);

        Number(box).KeyDown(Key.Up);

        Assert.Equal(6, box.Value!.As(LengthUnit.Foot), 9);
        Assert.Equal("6", Number(box).Text);
    }

    [Fact]
    public void The_number_is_shown_with_a_few_significant_digits_but_the_value_keeps_its_precision()
    {
        var box = CreateLength();
        var third = Length.FromMeters(1d / 3);

        box.Value = third;

        Assert.Equal("0.333333", Number(box).Text);
        Assert.Equal(third, box.Value);

        box.SignificantDigits = 3;

        Assert.Equal("0.333", Number(box).Text);

        box.Value = Length.FromMeters(123456);
        box.SignificantDigits = 2;

        Assert.Equal("120000", Number(box).Text);
    }

    [Fact]
    public void Limits_are_quantities_that_hold_in_every_unit()
    {
        var box = CreateLength();

        box.Maximum = Length.FromMeters(10);
        box.Minimum = Length.FromMeters(1);
        box.Value = Length.FromMeters(50);

        Assert.Equal(Length.FromMeters(10), box.Value);

        box.Value = Length.FromCentimeters(5);

        Assert.Equal(1, box.Value!.As(LengthUnit.Meter), 9);

        box.Unit = LengthUnit.Foot;
        Number(box).Text = string.Empty;
        Number(box).TextInput("99");

        Assert.Equal(10, box.Value!.As(LengthUnit.Meter), 9);
        Assert.Throws<ArgumentException>(() => box.Maximum = Mass.FromKilograms(1));
    }

    [Fact]
    public void Restricting_the_units_keeps_the_selection_when_possible_and_falls_back_to_the_base_unit()
    {
        var box = CreateLength();

        Assert.Throws<ArgumentException>(() => box.Units = [MassUnit.Gram]);

        box.Unit = LengthUnit.Foot;
        box.Units = [LengthUnit.Meter, LengthUnit.Foot];

        Assert.Equal(2, Dropdown(box).Items.Count);
        Assert.Equal(LengthUnit.Foot, box.Unit);

        box.Units = [LengthUnit.Meter, LengthUnit.Inch];

        Assert.Equal(LengthUnit.Meter, box.Unit);
        Assert.Throws<ArgumentException>(() => box.Unit = LengthUnit.Mile);
    }

    [Fact]
    public void The_theme_is_valid_against_the_default_theme_and_the_controls_of_this_assembly()
    {
        StylesheetValidator.AddControls(typeof(QuantityBox).Assembly);

        var issues = StylesheetValidator.ValidateText(QuantityTheme.Source(), QuantityTheme.FileName, [DefaultTheme.Load()]);

        Assert.Empty(issues);
    }
}
