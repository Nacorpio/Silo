using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Layout category.</summary>
    static IEnumerable<CatalogEntry> Layout() =>
    [
        new("TextBlock", "Layout", () => new TextBlock { Text = "Some text" }),
        new("Separator", "Layout", () => new Separator()),
        new("Expander", "Layout", CreateExpander),
        new("TabView", "Layout", CreateTabView),
        new("Toast", "Layout", CreateToasts),
    ];

    /// <summary>Creates a column of one Toast of each severity: a plain one, one with a title, one with an icon and an action, and one that failed.</summary>
    static Box CreateToasts()
    {
        var column = new Box();

        column.AddClass("toasts");

        column.Children.Add(new Toast { Message = "Settings saved", Severity = Severity.Success });
        column.Children.Add(new Toast { Message = "3 files changed", Title = "Heads up", Icon = Dot, ActionText = "Undo" });
        column.Children.Add(new Toast { Message = "Disk almost full", Severity = Severity.Warning });
        column.Children.Add(new Toast { Message = "Could not connect", Severity = Severity.Error });

        return column;
    }

    /// <summary>Creates a TabView with two tabs, so that a selected and an unselected header show.</summary>
    /// <returns>The TabView.</returns>
    static TabView CreateTabView()
    {
        var tabs = new TabView();

        tabs.AddTab("One", new TextBlock { Text = "First" }, Dot);
        tabs.AddTab("Two", new TextBlock { Text = "Second" }, Dot, closable: true);

        return tabs;
    }

    /// <summary>Creates an open Expander with some content, so that its header and its content show.</summary>
    /// <returns>The Expander.</returns>
    static Expander CreateExpander()
        => new() { Header = "Expander", Content = new TextBlock { Text = "Content" }, IsExpanded = true };
}
