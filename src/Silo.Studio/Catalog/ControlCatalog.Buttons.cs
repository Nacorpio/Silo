using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Buttons category.</summary>
    static IEnumerable<CatalogEntry> Buttons() =>
    [
        new("Button", "Buttons", () => new Button { Text = "Button" }),
        new("DropDownButton", "Buttons", () => new DropDownButton { Text = "Export", Entries = [new MenuItem("As PDF"), new MenuItem("As CSV")] }),
        new("SplitButton", "Buttons", () => new SplitButton { Text = "Save", Entries = [new MenuItem("Save As…")] }),
        new("RepeatButton", "Buttons", () => new RepeatButton { Text = "Hold" }),
        new("CheckBox", "Buttons", () => new CheckBox { Text = "Check" }),
        new("RadioButton", "Buttons", () => new RadioButton { Text = "Radio" }),
        new("ToggleSwitch", "Buttons", () => new ToggleSwitch { Text = "Toggle" }),
    ];
}
