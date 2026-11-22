namespace Silo.Studio;

/// <summary>
/// Every Control of the library in every Pseudo-state that applies to it, laid out as a table: one row per Control and one
/// column per state. It is what a theme author looks at to see what a Stylesheet does to the whole library at once.
/// </summary>
/// <remarks>
/// <para>
/// The cells hold real Controls with the Pseudo-state set on them, so the Stylesheet's <c>Button:hover</c> and
/// <c>CheckBox:checked</c> Rules apply exactly as they would to a Control that the pointer is over or that is checked. A
/// state that does not apply to a Control, such as <c>checked</c> for a Button, shows a dash.
/// </para>
/// <para>
/// The matrix styles itself with whatever resolver the window gives it, so switching the theme variant or editing the
/// theme file changes every cell.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var matrix = new ThemeMatrix();
/// var hovered = matrix.Cell("Button", "hover");   // a Button that has the hover Pseudo-state
/// </code>
/// </example>
public sealed class ThemeMatrix : Box
{
    /// <summary>The columns, in order. The names are the Pseudo-states that the matrix sets, with <c>normal</c> for none.</summary>
    public static readonly IReadOnlyList<string> States = ["normal", "hover", "pressed", "focus", "checked"];

    /// <summary>One row: what is shown and in which states it can be.</summary>
    /// <param name="Name">The row label, which is the type name that Selectors use.</param>
    /// <param name="Create">Creates a new sample Control.</param>
    /// <param name="States">The states that apply.</param>
    sealed record Row(string Name, Func<Control> Create, IReadOnlyList<string> States);

    /// <summary>The Controls that the matrix shows.</summary>
    static readonly Row[] Rows =
    [
        new("Button", () => new Button { Text = "Button" }, ["normal", "hover", "pressed", "focus"]),
        new("CheckBox", () => new CheckBox { Text = "Check" }, ["normal", "hover", "pressed", "focus", "checked"]),
        new("RadioButton", () => new RadioButton { Text = "Radio" }, ["normal", "hover", "pressed", "focus", "checked"]),
        new("ToggleSwitch", () => new ToggleSwitch { Text = "Toggle" }, ["normal", "hover", "pressed", "focus", "checked"]),
        new("TextBox", () => new TextBox { Placeholder = "Type here" }, ["normal", "hover", "focus"]),
        new("Slider", () => new Slider { Value = 0.6 }, ["normal", "hover", "pressed"]),
        new("ProgressBar", () => new ProgressBar { Value = 0.6 }, ["normal"]),
        new("ComboBox", () => new ComboBox { Items = ["Small", "Medium"], SelectedIndex = 1 }, ["normal", "hover", "focus"]),
        new("ListView", () => new ListView { Items = ["Row 1", "Row 2", "Row 3"], SelectedIndex = 1 }, ["normal", "hover"]),
        new("TreeView", CreateTreeView, ["normal", "hover", "focus"]),
        new("TextBlock", () => new TextBlock { Text = "Some text" }, ["normal"]),
        new("Separator", () => new Separator(), ["normal"]),
        new("Expander", CreateExpander, ["normal"]),
        new("TabView", CreateTabView, ["normal"]),
        new("MenuBar", CreateMenuBar, ["normal"]),
        new("ContextMenu", CreateContextMenu, ["normal"]),
        new("Dialog", CreateDialog, ["normal"]),
    ];

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

    /// <summary>Creates a ContextMenu that is not shown in a window, with a highlighted entry, a separator and a submenu arrow.</summary>
    /// <returns>The ContextMenu.</returns>
    static ContextMenu CreateContextMenu()
    {
        var entries = new List<MenuItem>
        {
            new("&Copy"),
            new("&Paste"),
            MenuItem.Separator,
            new("&Recent") { Children = [new MenuItem("a.txt")] },
        };

        return new ContextMenu { Entries = entries, SelectedIndex = 1 };
    }

    /// <summary>Creates a MenuBar with two menus.</summary>
    /// <returns>The MenuBar.</returns>
    static MenuBar CreateMenuBar()
    {
        var bar = new MenuBar();

        bar.AddMenu("&File", [new MenuItem("Open"), new MenuItem("Exit")]);
        bar.AddMenu("&Help", [new MenuItem("About")]);

        return bar;
    }

    /// <summary>Creates a Dialog that is not shown in a window, so that its title, content and buttons show in the matrix.</summary>
    /// <returns>The Dialog.</returns>
    static Dialog CreateDialog()
    {
        var dialog = new Dialog { Title = "Dialog", Content = new TextBlock { Text = "Are you sure?" } };

        dialog.AddButton("Cancel");
        dialog.AddButton("OK");

        return dialog;
    }

    /// <summary>Creates a TabView with two tabs, so that a selected and an unselected header show.</summary>
    /// <returns>The TabView.</returns>
    static TabView CreateTabView()
    {
        var tabs = new TabView();

        tabs.AddTab("One", new TextBlock { Text = "First" });
        tabs.AddTab("Two", new TextBlock { Text = "Second" }, closable: true);

        return tabs;
    }

    /// <summary>Creates an open Expander with some content, so that its header and its content show.</summary>
    /// <returns>The Expander.</returns>
    static Expander CreateExpander()
        => new() { Header = "Expander", Content = new TextBlock { Text = "Content" }, IsExpanded = true };

    /// <summary>Raised when the label of a row is clicked, with the row name, so an editor can show the Rule that styles it.</summary>
    public event EventHandler<string>? RowSelected;


    /// <summary>The sample Control of each cell, by row and state.</summary>
    readonly Dictionary<(string Row, string State), Control> cells = [];

    /// <summary>Creates the matrix with all of its rows.</summary>
    public ThemeMatrix()
    {
        Name = "matrix";

        Children.Add(HeaderRow());

        foreach (var row in Rows)
            Children.Add(BuildRow(row));
    }

    /// <summary>Gets the names of the rows, in order.</summary>
    public static IEnumerable<string> RowNames => Rows.Select(r => r.Name);

    /// <summary>Gets the sample Control in a cell.</summary>
    /// <param name="row">The row name, such as <c>Button</c>.</param>
    /// <param name="state">The column, one of <see cref="States"/>.</param>
    /// <returns>The Control, or <see langword="null"/> when the state does not apply to the row.</returns>
    public Control? Cell(string row, string state) => cells.GetValueOrDefault((row, state));

    /// <summary>Builds the row of column titles.</summary>
    /// <returns>The row.</returns>
    static Box HeaderRow()
    {
        var header = new Box();

        header.AddClass("matrix-row");
        header.Children.Add(Label(string.Empty, "row-label"));

        foreach (var state in States)
            header.Children.Add(Label(state, "column-title"));

        return header;
    }

    /// <summary>Builds the row of one Control.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The row Control.</returns>
    Box BuildRow(Row row)
    {
        var result = new Box();

        result.AddClass("matrix-row");
        result.Children.Add(RowButton(row.Name));

        foreach (var state in States)
            result.Children.Add(BuildCell(row, state));

        return result;
    }

    /// <summary>Builds the label of a row, which can be clicked.</summary>
    /// <param name="name">The row name.</param>
    /// <returns>The button.</returns>
    Button RowButton(string name)
    {
        var button = new Button { Text = name, Name = $"row-{name}" };

        button.AddClass("row-label");

        button.Click += (_, _) => RowSelected?.Invoke(this, name);

        return button;
    }

    /// <summary>Builds one cell.</summary>
    /// <param name="row">The row.</param>
    /// <param name="state">The column.</param>
    /// <returns>The cell Control.</returns>
    Box BuildCell(Row row, string state)
    {
        var cell = new Box();

        cell.AddClass("cell");

        if (!row.States.Contains(state))
        {
            cell.Children.Add(Label("-", "not-applicable"));

            return cell;
        }

        var sample = row.Create();

        Apply(sample, state);

        cells[(row.Name, state)] = sample;
        cell.Children.Add(sample);

        return cell;
    }

    /// <summary>Puts a Control into a state.</summary>
    /// <param name="sample">The Control.</param>
    /// <param name="state">One of <see cref="States"/>.</param>
    static void Apply(Control sample, string state)
    {
        switch (state)
        {
            case "hover":
                sample.SetHovered(true);
                break;

            case "pressed":
                sample.SetPressed(true);
                break;

            case "focus":
                sample.SetFocused(true);
                break;

            case "checked":
                ((ToggleButton)sample).IsChecked = true;
                break;
        }
    }

    /// <summary>Creates a text label with a Class.</summary>
    /// <param name="text">The text.</param>
    /// <param name="className">The Class.</param>
    /// <returns>The label.</returns>
    static TextBlock Label(string text, string className)
    {
        var label = new TextBlock { Text = text };

        label.AddClass(className);

        return label;
    }
}
