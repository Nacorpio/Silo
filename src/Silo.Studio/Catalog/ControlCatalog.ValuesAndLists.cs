using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Values and lists category.</summary>
    static IEnumerable<CatalogEntry> ValuesAndLists() =>
    [
        new("Slider", "Values and lists", () => new Slider { Value = 0.6 }),
        new("ProgressBar", "Values and lists", () => new ProgressBar { Value = 0.6 }),
        new("ProgressRing", "Values and lists", CreateProgressRings),
        new("ComboBox", "Values and lists", () => new ComboBox { Items = ["Small", "Medium"], SelectedIndex = 1 }),
        new("CheckedListBox", "Values and lists", CreateCheckedListBox),
    ];

    /// <summary>Creates a row of a spinning ring, a ring that is 65 percent full, and a ring that is off.</summary>
    static Box CreateProgressRings()
    {
        var row = new Box();

        row.SetValue(Control.FlexDirectionProperty, Silo.Layout.FlexDirection.Row);
        row.SetValue(Control.GapProperty, new Silo.Styling.Values.Length(12, Silo.Styling.Values.LengthUnit.Px));

        row.Children.Add(new ProgressRing());
        row.Children.Add(new ProgressRing { Value = 0.65 });
        row.Children.Add(new ProgressRing { IsActive = false });

        return row;
    }

    /// <summary>Creates a CheckedListBox with two checked rows.</summary>
    static CheckedListBox CreateCheckedListBox()
    {
        var list = new CheckedListBox { Items = ["Cheese", "Olives", "Onions", "Peppers"], SelectedIndex = 1 };

        list.SetChecked(0, true);
        list.SetChecked(2, true);

        return list;
    }
}
