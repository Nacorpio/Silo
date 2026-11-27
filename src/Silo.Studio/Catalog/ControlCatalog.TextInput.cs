using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Text input category.</summary>
    static IEnumerable<CatalogEntry> TextInput() =>
    [
        new("TextBox", "Text input", () => new TextBox { Placeholder = "Type here" }),
        new("NumberBox", "Text input", () => new NumberBox { Minimum = 0, Maximum = 100, Value = 42 }),
        new("PasswordBox", "Text input", () => new PasswordBox { Text = "secret" }),
        new("QuantityBox", "Text input", () => new QuantityBox { QuantityType = "Length", Value = Length.FromMeters(2.5) }),
        new("SearchBox", "Text input", () => new SearchBox { Text = "button" }),
        new("TextArea", "Text input", CreateTextArea),
    ];

    /// <summary>Creates a TextArea with three lines of text.</summary>
    /// <returns>The TextArea.</returns>
    static TextArea CreateTextArea() => new(new TextBuffer("First line\nSecond line\nThird line"));
}
