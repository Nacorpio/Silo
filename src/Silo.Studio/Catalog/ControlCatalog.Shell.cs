using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Shell category.</summary>
    static IEnumerable<CatalogEntry> Shell() =>
    [
        new("StatusBar", "Shell", CreateStatusBar),
        new("NavigationView", "Shell", CreateNavigationView),
        new("TitleBar", "Shell", () => new TitleBar { Title = "Silo Studio", Icon = Dot }),
    ];

    /// <summary>Creates a StatusBar with a text on each side and one in the middle.</summary>
    static StatusBar CreateStatusBar()
    {
        var bar = new StatusBar();

        bar.AddItem("Ready");
        bar.AddItem("main.cs", bar.Center);
        bar.AddItem("Ln 12, Col 4", bar.Right);

        return bar;
    }

    /// <summary>Creates a NavigationView with a header, three destinations and a page.</summary>
    static NavigationView CreateNavigationView()
        => new()
        {
            Items = [new("Home", "home"), new("Library", IsHeader: true), new("Search", "search"), new("Settings", "menu")],
            SelectedIndex = 2,
            Content = new TextBlock { Text = "Search page" },
        };
}
