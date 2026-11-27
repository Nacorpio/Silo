using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Data category.</summary>
    static IEnumerable<CatalogEntry> Data() =>
    [
        new("DataGrid", "Data", CreateDataGrid),
        new("ListView", "Data", () => new ListView { Items = ["Row 1", "Row 2", "Row 3"], SelectedIndex = 1 }),
        new("TreeView", "Data", CreateTreeView),
    ];

    /// <summary>Creates a DataGrid with three columns, a sort and a selected row.</summary>
    static DataGrid CreateDataGrid()
    {
        var grid = new DataGrid
        {
            Rows = new (string Name, string Kind, double Size)[]
            {
                ("Button", "Buttons", 12.5),
                ("Slider", "Values", 8),
                ("TabView", "Layout", 31.25),
                ("Dialog", "Menus", 20),
            }.Select(row => (object)row).ToList(),
        };

        grid.Columns.Add(new DataGridColumn("Name", row => ((ValueTuple<string, string, double>)row).Item1) { Width = 80, Icon = Dot, IconBinding = _ => Dot });
        grid.Columns.Add(new DataGridColumn("Kind", row => ((ValueTuple<string, string, double>)row).Item2) { Width = 60 });
        grid.Columns.Add(new DataGridColumn("Size", row => ((ValueTuple<string, string, double>)row).Item3) { Width = 68, Format = "N1", Alignment = TextAlign.Right });

        grid.SortBy(grid.Columns[2]);

        grid.SelectedIndex = 1;

        return grid;
    }

    /// <summary>Creates a TreeView with an expanded node, a collapsed node and a selected leaf.</summary>
    /// <returns>The TreeView.</returns>
    static TreeView CreateTreeView()
    {
        var tree = new TreeView();
        var src = tree.Add(new TreeNode("src") { IsExpanded = true });

        src.Add(new TreeNode("Program.cs"));
        src.Add(new TreeNode("Views")).Add(new TreeNode("Main.cs"));

        tree.Add(new TreeNode("docs")).Add(new TreeNode("index.md"));

        tree.SelectedNode = src.Children[0];

        return tree;
    }
}
