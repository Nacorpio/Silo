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
/// The rows are grouped into tab pages by category, so that one page is small enough to judge in a screenshot.
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
    public static IReadOnlyList<string> States => ControlCatalog.Columns;

    /// <summary>The name of the last category, which holds the animation playground instead of rows.</summary>
    public const string AnimationsCategory = "Animations";

    /// <summary>Raised when the label of a row is clicked, with the row name, so an editor can show the Rule that styles it.</summary>
    public event EventHandler<string>? RowSelected;


    /// <summary>The sample Control of each cell, by row and state.</summary>
    readonly Dictionary<(string Row, string State), Control> cells = [];

    /// <summary>The page of each category.</summary>
    readonly Dictionary<string, Box> pages = [];

    /// <summary>The category that is shown.</summary>
    string selectedCategory;

    /// <summary>Creates the matrix with all of its rows.</summary>
    public ThemeMatrix()
    {
        Name = "matrix";

        foreach (var category in ControlCatalog.Categories)
            pages[category] = BuildPage(category);

        pages[AnimationsCategory] = new Box { Name = $"page-{AnimationsCategory}", Children = { Playground } };

        selectedCategory = Categories.First();

        Children.Add(pages[selectedCategory]);
    }

    /// <summary>Raised after another category was shown.</summary>
    public event EventHandler? CategoryChanged;

    /// <summary>Gets the names of the categories, in order. Each holds the rows of one group of Controls.</summary>
    public static IEnumerable<string> Categories => ControlCatalog.Categories.Append(AnimationsCategory);

    /// <summary>Gets the animation playground, which is the page of the Animations category.</summary>
    public AnimationPlayground Playground => playground ??= new AnimationPlayground();

    AnimationPlayground? playground;

    /// <summary>Gets the glyph, the short description and the number of Controls of a category, for the list beside the matrix.</summary>
    /// <param name="category">One of <see cref="Categories"/>.</param>
    /// <returns>The facts about the category.</returns>
    public static CategoryInfo Describe(string category)
    {
        var info = ControlCatalog.Describe(category);

        return category == AnimationsCategory ? info with { Count = 1 } : info;
    }

    /// <summary>Gets the category that is shown.</summary>
    public string SelectedCategory => selectedCategory;

    /// <summary>Gets the page of the category that is shown, which holds its rows.</summary>
    public Box Page => pages[selectedCategory];

    /// <summary>Shows the page of a category, so that its rows are on screen.</summary>
    /// <param name="category">One of <see cref="Categories"/>.</param>
    /// <returns><see langword="true"/> when the page was not shown yet, so styles need to be applied again.</returns>
    /// <exception cref="ArgumentException">There is no such category.</exception>
    public bool ShowCategory(string category)
    {
        if (!pages.TryGetValue(category, out var page))
            throw new ArgumentException($"There is no category named '{category}'.", nameof(category));

        if (category == selectedCategory)
            return false;

        selectedCategory = category;

        Children.Clear();
        Children.Add(page);

        CategoryChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Gets the names of the rows, in order.</summary>
    public static IEnumerable<string> RowNames => ControlCatalog.Entries.Select(e => e.Name);

    /// <summary>Gets the sample Control in a cell.</summary>
    /// <param name="row">The row name, such as <c>Button</c>.</param>
    /// <param name="state">The column, one of <see cref="States"/>.</param>
    /// <returns>The Control, or <see langword="null"/> when the state does not apply to the row.</returns>
    public Control? Cell(string row, string state) => cells.GetValueOrDefault((row, state));

    /// <summary>Builds the page of a category: the column titles and one row for each of its Controls.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The page.</returns>
    Box BuildPage(string category)
    {
        var page = new Box { Name = $"page-{category}" };

        page.AddClass("matrix-page");
        page.Children.Add(HeaderRow());

        foreach (var row in ControlCatalog.Entries.Where(e => e.Category == category))
            page.Children.Add(BuildRow(row));

        return page;
    }

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
    Box BuildRow(CatalogEntry row)
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
    Box BuildCell(CatalogEntry row, string state)
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
    internal static void Apply(Control sample, string state)
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
