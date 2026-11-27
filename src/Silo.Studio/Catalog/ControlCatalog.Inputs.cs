using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Inputs category.</summary>
    static IEnumerable<CatalogEntry> Inputs() =>
    [
        new("SegmentedControl", "Inputs", () => new SegmentedControl { Items = ["Day", "Week", "Month"], SelectedIndex = 1 }),
        new("RadioGroup", "Inputs", CreateRadioGroup),
        new("Rating", "Inputs", () => new Rating { Value = 3, AllowHalf = true }),
        new("RangeSlider", "Inputs", () => new RangeSlider { Minimum = 0, Maximum = 100, Lower = 20, Upper = 70 }),
        new("TokenBox", "Inputs", CreateTokenBox),
        new("FormField", "Inputs", CreateFormFields),
        new("EmojiPicker", "Inputs", () => new EmojiPicker { SelectedEmoji = "\U0001F600" }),
        new("EmojiPanel", "Inputs", () => new EmojiPanel { Recent = ["\U0001F44D", "\U0001F602", "\u2764\uFE0F"] }),
    ];

    /// <summary>Creates a RadioGroup with three options, the second on.</summary>
    static RadioGroup CreateRadioGroup()
    {
        var group = new RadioGroup();

        group.Add("Small");
        group.Add("Medium");
        group.Add("Large");

        group.SelectedIndex = 1;

        return group;
    }

    /// <summary>Creates a TokenBox with two tokens.</summary>
    static TokenBox CreateTokenBox()
    {
        var box = new TokenBox();

        box.Add("bug");
        box.Add("docs");
        box.Entry.Placeholder = "Add";

        return box;
    }

    /// <summary>Creates a column of a FormField with help text and one with an error.</summary>
    static Box CreateFormFields()
    {
        var column = new Box();

        column.AddClass("forms");

        column.Children.Add(new FormField { Label = "Name", Content = new TextBox { Text = "Ada" }, HelpText = "Your full name", IsRequired = true });
        column.Children.Add(new FormField { Label = "Age", Content = new TextBox { Text = "12" }, Error = "Must be an adult" });

        return column;
    }
}
