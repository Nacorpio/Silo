using Silo.Styling;
using Silo.Styling.Values;

namespace Silo.Inspector;

/// <summary>
/// The Inspector's contents: the Element tree on the left and the Styled properties of the selected Element on the right.
/// </summary>
/// <remarks>
/// <para>
/// The view only shows what it is given. A controller feeds it with <see cref="SetTree"/> and <see cref="SetProperties"/>,
/// and listens to <see cref="NodeSelected"/> to learn what to ask for next. Because it is built from Silo Controls, it
/// uses its own Stylesheet and ignores the app's Theme, so inspecting an app never changes how the Inspector looks.
/// </para>
/// <para>
/// The view does not scroll yet, since ScrollViewer is a later Control, so long trees and property lists are cut off at
/// the bottom of the window.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var view = new InspectorView();
///
/// view.SetTree(InspectorNode.Parse(snapshot));
/// view.NodeSelected += id => Console.WriteLine($"Selected {id}");
/// </code>
/// </example>
public sealed class InspectorView : Box
{
    /// <summary>The number of pixels that the tree indents each level of depth.</summary>
    const double IndentPixels = 14;


    /// <summary>The Inspector's own Stylesheet.</summary>
    const string Sheet = """
        {
          "rules": {
            "#inspector": { "background": "#111827", "color": "#e5e7eb", "padding": "8px", "gap": "16px", "font-size": "13px", "flex-direction": "row" },
            "#tree": { "flex-direction": "column", "gap": "2px", "width": "360px" },
            "#details": { "flex-direction": "column", "gap": "3px", "flex-grow": "1" },
            "Button.row": {
              "background": "#111827", "color": "#d1d5db", "padding": "3px 6px", "text-align": "left", "border-radius": "3px",
              "&:hover": { "background": "#1f2937" },
              "&:active": { "background": "#374151" }
            },
            "Button.row.selected": { "background": "#2563eb", "color": "#ffffff" },
            ".title": { "color": "#ffffff", "font-weight": "bold", "font-size": "15px" },
            ".origin-local": { "color": "#fbbf24" },
            ".origin-stylesheet": { "color": "#93c5fd" },
            ".origin-default": { "color": "#6b7280" }
          }
        }
        """;


    /// <summary>The Stylesheet resolver for the view.</summary>
    readonly StyleResolver styles = new([JsoncStylesheetReader.Read(Sheet, "inspector.jsonc")]);

    /// <summary>The column that lists the Elements.</summary>
    readonly Box tree = new() { Name = "tree" };

    /// <summary>The column that lists the properties of the selected Element.</summary>
    readonly Box details = new() { Name = "details" };

    /// <summary>The row of each Element, by the Element's id.</summary>
    readonly Dictionary<int, Button> rows = [];

    /// <summary>Creates an empty view.</summary>
    public InspectorView()
    {
        Name = "inspector";

        Children.Add(tree);
        Children.Add(details);

        Restyle();
    }

    /// <summary>Raised with the id of an Element when its row is clicked.</summary>
    public event Action<int>? NodeSelected;

    /// <summary>Raised after the view changed, so that the host knows to draw a new frame.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the number of Elements in the tree list.</summary>
    public int RowCount => rows.Count;

    /// <summary>Replaces the tree list.</summary>
    /// <param name="root">The root of the tree to show.</param>
    public void SetTree(InspectorNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        tree.Children.Clear();
        rows.Clear();

        AddRows(root, 0);
        Restyle();
    }

    /// <summary>Shows the properties of an Element and highlights its row.</summary>
    /// <param name="id">The id of the Element.</param>
    /// <param name="title">The heading, usually the Element's label.</param>
    /// <param name="properties">The properties to list.</param>
    public void SetProperties(int id, string title, IReadOnlyList<InspectorProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        foreach (var (rowId, row) in rows)
            if (rowId == id)
                row.AddClass("selected");
            else
                row.RemoveClass("selected");

        details.Children.Clear();
        details.Children.Add(Line(title, "title"));

        foreach (var property in properties)
            details.Children.Add(Line(property.Label, $"origin-{property.Origin}"));

        Restyle();
    }

    /// <summary>Adds the row of a node and then the rows of its descendants.</summary>
    /// <param name="node">The node.</param>
    /// <param name="depth">How deep the node is, which sets its indent.</param>
    void AddRows(InspectorNode node, int depth)
    {
        var row = new Button { Text = node.Label };
        var indent = new Length(depth * IndentPixels, LengthUnit.Px);
        var none = new Length(0, LengthUnit.Px);

        row.AddClass("row");
        row.SetValue(MarginProperty, new Thickness(none, none, none, indent));
        row.Click += (_, _) => NodeSelected?.Invoke(node.Id);

        rows[node.Id] = row;
        tree.Children.Add(row);

        foreach (var child in node.Children)
            AddRows(child, depth + 1);
    }

    /// <summary>Creates a line of text with a Class.</summary>
    /// <param name="text">The text.</param>
    /// <param name="className">The Class.</param>
    /// <returns>The line.</returns>
    static TextBlock Line(string text, string className)
    {
        var line = new TextBlock { Text = text };

        line.AddClass(className);

        return line;
    }

    /// <summary>Applies the view's Stylesheet and tells the host.</summary>
    void Restyle()
    {
        ApplyStyles(styles);

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
