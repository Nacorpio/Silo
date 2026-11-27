using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Colors category.</summary>
    static IEnumerable<CatalogEntry> Colors() =>
    [
        new("ColorPicker", "Colors", () => new ColorPicker { Color = new Color(59, 130, 246) }),
        new("ColorPanel", "Colors", () => new ColorPanel { Color = new Color(236, 72, 153), ShowAlpha = true }),
    ];
}
