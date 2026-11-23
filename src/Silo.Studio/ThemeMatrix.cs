using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

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
    public static readonly IReadOnlyList<string> States = ["normal", "hover", "pressed", "focus", "checked"];

    /// <summary>The glyph and the description of each category.</summary>
    static readonly Dictionary<string, (string Glyph, string Description)> Details = new()
    {
        ["Buttons"] = ("check", "Buttons, toggles and radios"),
        ["Text input"] = ("pencil", "Text boxes and search"),
        ["Values and lists"] = ("list", "Sliders, numbers and lists"),
        ["Inputs"] = ("sliders", "Segments, ratings, token boxes"),
        ["Data"] = ("grid", "Grids and trees"),
        ["Dates"] = ("calendar", "Calendar and date picker"),
        ["Colors"] = ("eye", "Color panel and picker"),
        ["Display"] = ("info", "Labels, badges, avatars, stats"),
        ["Panels"] = ("layout", "Cards, groups and expanders"),
        ["Navigation"] = ("arrow-right", "Breadcrumbs, pagers, steppers"),
        ["Shell"] = ("home", "Status bars and navigation panes"),
        ["Collections"] = ("image", "Thumbnail and card galleries"),
        ["Layout"] = ("layout", "Separators and containers"),
        ["Menus and dialogs"] = ("menu", "Menus, dialogs, tooltips, toasts"),
    };

    /// <summary>The icon that <see cref="Dot"/> creates once.</summary>
    static SkiaImage? dot;


    /// <summary>One row: what is shown and in which states it can be.</summary>
    /// <param name="Name">The row label, which is the type name that Selectors use.</param>
    /// <param name="Category">The name of the tab page that holds the row.</param>
    /// <param name="Create">Creates a new sample Control.</param>
    /// <param name="States">The states that apply.</param>
    sealed record Row(string Name, string Category, Func<Control> Create, IReadOnlyList<string> States);

    /// <summary>The Controls that the matrix shows.</summary>
    static readonly Row[] Rows =
    [
        new("Button", "Buttons", () => new Button { Text = "Button" }, ["normal", "hover", "pressed", "focus"]),
        new("CheckBox", "Buttons", () => new CheckBox { Text = "Check" }, ["normal", "hover", "pressed", "focus", "checked"]),
        new("RadioButton", "Buttons", () => new RadioButton { Text = "Radio" }, ["normal", "hover", "pressed", "focus", "checked"]),
        new("ToggleSwitch", "Buttons", () => new ToggleSwitch { Text = "Toggle" }, ["normal", "hover", "pressed", "focus", "checked"]),
        new("TextBox", "Text input", () => new TextBox { Placeholder = "Type here" }, ["normal", "hover", "focus"]),
        new("NumberBox", "Text input", () => new NumberBox { Minimum = 0, Maximum = 100, Value = 42 }, ["normal", "hover", "focus"]),
        new("PasswordBox", "Text input", () => new PasswordBox { Text = "secret" }, ["normal", "hover", "focus"]),
        new("QuantityBox", "Text input", () => new QuantityBox { QuantityType = "Length", Value = Length.FromMeters(2.5) }, ["normal", "hover", "focus"]),
        new("SearchBox", "Text input", () => new SearchBox { Text = "button" }, ["normal", "hover", "focus"]),
        new("TextArea", "Text input", CreateTextArea, ["normal", "hover", "focus"]),
        new("Slider", "Values and lists", () => new Slider { Value = 0.6 }, ["normal", "hover", "pressed"]),
        new("ProgressBar", "Values and lists", () => new ProgressBar { Value = 0.6 }, ["normal"]),
        new("ComboBox", "Values and lists", () => new ComboBox { Items = ["Small", "Medium"], SelectedIndex = 1 }, ["normal", "hover", "focus"]),
        new("CheckedListBox", "Values and lists", CreateCheckedListBox, ["normal", "focus"]),
        new("SegmentedControl", "Inputs", () => new SegmentedControl { Items = ["Day", "Week", "Month"], SelectedIndex = 1 }, ["normal"]),
        new("RadioGroup", "Inputs", CreateRadioGroup, ["normal"]),
        new("Rating", "Inputs", () => new Rating { Value = 3, AllowHalf = true }, ["normal", "hover", "focus"]),
        new("RangeSlider", "Inputs", () => new RangeSlider { Minimum = 0, Maximum = 100, Lower = 20, Upper = 70 }, ["normal", "hover", "pressed"]),
        new("TokenBox", "Inputs", CreateTokenBox, ["normal", "focus"]),
        new("FormField", "Inputs", CreateFormFields, ["normal"]),
        new("DataGrid", "Data", CreateDataGrid, ["normal", "focus"]),
        new("ListView", "Data", () => new ListView { Items = ["Row 1", "Row 2", "Row 3"], SelectedIndex = 1 }, ["normal", "hover"]),
        new("TreeView", "Data", CreateTreeView, ["normal", "hover", "focus"]),
        new("DatePicker", "Dates", () => new DatePicker { SelectedDate = new DateOnly(2026, 11, 23) }, ["normal", "hover", "focus"]),
        new("Calendar", "Dates", CreateCalendar, ["normal"]),
        new("ColorPicker", "Colors", () => new ColorPicker { Color = new Color(59, 130, 246) }, ["normal", "hover", "focus"]),
        new("ColorPanel", "Colors", () => new ColorPanel { Color = new Color(236, 72, 153), ShowAlpha = true }, ["normal"]),
        new("Label", "Display", CreateLabels, ["normal"]),
        new("Badge", "Display", CreateBadges, ["normal"]),
        new("Avatar", "Display", CreateAvatars, ["normal"]),
        new("StatList", "Display", CreateStatList, ["normal"]),
        new("Icon", "Display", CreateIcons, ["normal"]),
        new("HyperlinkButton", "Display", () => new HyperlinkButton { Text = "Read the docs" }, ["normal", "hover", "pressed", "focus"]),
        new("GroupBox", "Panels", () => new GroupBox { Header = "Options", Content = new TextBlock { Text = "Wrap lines" } }, ["normal"]),
        new("Card", "Panels", CreateCard, ["normal"]),
        new("InfoBar", "Panels", CreateInfoBars, ["normal"]),
        new("Breadcrumb", "Navigation", () => new Breadcrumb { Items = ["Home", "Projects", "Silo", "Readme"] }, ["normal"]),
        new("Pager", "Navigation", () => new Pager { PageCount = 20, CurrentPage = 10 }, ["normal"]),
        new("Accordion", "Navigation", CreateAccordion, ["normal"]),
        new("StatusBar", "Shell", CreateStatusBar, ["normal"]),
        new("Stepper", "Navigation", CreateStepper, ["normal"]),
        new("Carousel", "Navigation", CreateCarousel, ["normal"]),
        new("NavigationView", "Shell", CreateNavigationView, ["normal"]),
        new("GalleryView grid", "Collections", () => CreateGallery(GalleryLayout.Grid), ["normal"]),
        new("GalleryView justified", "Collections", () => CreateGallery(GalleryLayout.Justified, false), ["normal"]),
        new("GalleryView masonry", "Collections", () => CreateGallery(GalleryLayout.Masonry, false), ["normal"]),
        new("GalleryView strip", "Collections", () => CreateGallery(GalleryLayout.Strip), ["normal"]),
        new("TextBlock", "Layout", () => new TextBlock { Text = "Some text" }, ["normal"]),
        new("Separator", "Layout", () => new Separator(), ["normal"]),
        new("Expander", "Layout", CreateExpander, ["normal"]),
        new("TabView", "Layout", CreateTabView, ["normal"]),
        new("Toast", "Layout", CreateToasts, ["normal"]),
        new("Tooltip", "Menus and dialogs", CreateTooltips, ["normal"]),
        new("Toolbar", "Menus and dialogs", CreateToolbar, ["normal"]),
        new("MenuBar", "Menus and dialogs", CreateMenuBar, ["normal"]),
        new("ContextMenu", "Menus and dialogs", CreateContextMenu, ["normal"]),
        new("Dialog", "Menus and dialogs", CreateDialog, ["normal"]),
    ];

    /// <summary>Creates a Calendar that shows a fixed month with a selected date, so the gallery looks the same every day.</summary>
    static Calendar CreateCalendar()
    {
        var calendar = new Calendar
        {
            Culture = CultureInfo.InvariantCulture,
            FirstDayOfWeek = DayOfWeek.Monday,
            Today = new DateOnly(2026, 11, 23),
            DisplayedMonth = new DateOnly(2026, 11, 1),
            IsCursorVisible = false,
        };

        calendar.SelectedDate = new DateOnly(2026, 11, 12);

        return calendar;
    }

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

    /// <summary>Creates a column of a plain Label and a required one.</summary>
    static Box CreateLabels()
    {
        var column = new Box();

        column.AddClass("stack");

        column.Children.Add(new Label { Text = "Name" });

        var required = new Label { Text = "Email" };

        required.AddClass("required");
        column.Children.Add(required);

        return column;
    }

    /// <summary>Creates a row of Badges: a count, a count above the maximum and one of each severity.</summary>
    static Box CreateBadges()
    {
        var row = new Box();

        row.AddClass("badges");

        row.Children.Add(new Badge { Value = 7 });
        row.Children.Add(new Badge { Value = 120 });
        row.Children.Add(new Badge { Text = "New", Severity = Severity.Info });
        row.Children.Add(new Badge { Text = "OK", Severity = Severity.Success });
        row.Children.Add(new Badge { Text = "Low", Severity = Severity.Warning });
        row.Children.Add(new Badge { Text = "Fail", Severity = Severity.Error });

        return row;
    }

    /// <summary>Creates a row of Avatars with initials, and one with a picture.</summary>
    static Box CreateAvatars()
    {
        var row = new Box();

        row.AddClass("badges");

        row.Children.Add(new Avatar { DisplayName = "Ada Lovelace" });
        row.Children.Add(new Avatar { DisplayName = "Grace Hopper" });
        row.Children.Add(new Avatar { DisplayName = "Linus" });
        row.Children.Add(new Avatar { DisplayName = "Alan Turing" });

        return row;
    }

    /// <summary>Creates a row with one Icon for each built-in glyph.</summary>
    static Box CreateIcons()
    {
        var row = new Box();

        row.AddClass("icons");

        foreach (var name in Glyphs.Names)
            row.Children.Add(new Icon { Glyph = name });

        return row;
    }

    /// <summary>Creates a Card with a header, content and a footer.</summary>
    static Card CreateCard() => new()
    {
        Header = new TextBlock { Text = "Build 42" },
        Content = new TextBlock { Text = "Passed in 3 minutes" },
        Footer = new TextBlock { Text = "2 hours ago" },
    };

    /// <summary>Creates a column of one InfoBar of each severity.</summary>
    static Box CreateInfoBars()
    {
        var column = new Box();

        column.AddClass("infobars");

        column.Children.Add(new InfoBar { Title = "Update", Message = "Version 2 is ready", ActionText = "Restart", IsClosable = true });
        column.Children.Add(new InfoBar { Message = "Settings saved", Severity = Severity.Success });
        column.Children.Add(new InfoBar { Message = "Disk almost full", Severity = Severity.Warning, IsClosable = true });
        column.Children.Add(new InfoBar { Title = "Error", Message = "Could not connect", Severity = Severity.Error });

        return column;
    }

    /// <summary>Creates a RadioGroup with three options, the second on.</summary>
    static RadioGroup CreateRadioGroup()
    {
        var group = new RadioGroup();

        group.Add("Small");
        group.Add("Medium");
        group.Add("Large");

        group.SelectedIndex = 1;

        return group;
    }

    /// <summary>Creates a TokenBox with two tokens.</summary>
    static TokenBox CreateTokenBox()
    {
        var box = new TokenBox();

        box.Add("bug");
        box.Add("docs");
        box.Entry.Placeholder = "Add";

        return box;
    }

    /// <summary>Creates a column of a FormField with help text and one with an error.</summary>
    static Box CreateFormFields()
    {
        var column = new Box();

        column.AddClass("forms");

        column.Children.Add(new FormField { Label = "Name", Content = new TextBox { Text = "Ada" }, HelpText = "Your full name", IsRequired = true });
        column.Children.Add(new FormField { Label = "Age", Content = new TextBox { Text = "12" }, Error = "Must be an adult" });

        return column;
    }

    /// <summary>Creates a CheckedListBox with two checked rows.</summary>
    static CheckedListBox CreateCheckedListBox()
    {
        var list = new CheckedListBox { Items = ["Cheese", "Olives", "Onions", "Peppers"], SelectedIndex = 1 };

        list.SetChecked(0, true);
        list.SetChecked(2, true);

        return list;
    }

    /// <summary>Creates a column of a plain Tooltip and one with an icon, a header, a shortcut and a description.</summary>
    static Box CreateTooltips()
    {
        var column = new Box();

        column.AddClass("tooltips");

        column.Children.Add(new Tooltip { Text = "Save the document", Shortcut = "Ctrl+S" });
        column.Children.Add(new Tooltip
        {
            Glyph = "info",
            Header = "Save",
            Shortcut = "Ctrl+S",
            Text = "Write the document to disk",
            Description = "Saves to the file the document was opened from. Unsaved changes are kept in a backup.",
        });

        return column;
    }

    /// <summary>Creates an Accordion with three sections, the second open.</summary>
    static Accordion CreateAccordion()
    {
        var accordion = new Accordion();

        accordion.Add("General", new TextBlock { Text = "Name and language" });
        accordion.Add("Appearance", new TextBlock { Text = "Theme and font" });
        accordion.Add("Advanced", new TextBlock { Text = "Cache and logs" });

        accordion.ExpandedIndex = 1;

        return accordion;
    }

    /// <summary>Creates a StatusBar with a text on each side and one in the middle.</summary>
    static StatusBar CreateStatusBar()
    {
        var bar = new StatusBar();

        bar.AddItem("Ready");
        bar.AddItem("main.cs", bar.Center);
        bar.AddItem("Ln 12, Col 4", bar.Right);

        return bar;
    }

    /// <summary>Creates a Stepper at its second step.</summary>
    static Stepper CreateStepper()
        => new()
        {
            Steps = [new("Account", new TextBlock { Text = "Choose a name" }), new("Plan", new TextBlock { Text = "Pick a plan" }), new("Done", new TextBlock { Text = "All set" })],
            CurrentStep = 1,
        };

    /// <summary>Creates a Carousel of three texts at the second one.</summary>
    static Carousel CreateCarousel()
        => new() { Items = [new TextBlock { Text = "First slide" }, new TextBlock { Text = "Second slide" }, new TextBlock { Text = "Third slide" }], SelectedIndex = 1 };

    /// <summary>Creates a StatList with the numbers of a repository.</summary>
    static StatList CreateStatList()
        => new()
        {
            Items =
            [
                new("activity", Label: "Activity"),
                new("star", "1", "star"),
                new("eye", "0", "watching"),
                new("fork", "0", "forks"),
            ],
        };

    /// <summary>Creates a GalleryView of colored tiles in different shapes, the third one selected.</summary>
    /// <param name="layout">The layout to show them in.</param>
    /// <param name="captions">Whether the tiles have a title and subtitle, which take room from the picture.</param>
    static GalleryView CreateGallery(GalleryLayout layout, bool captions = true)
    {
        var gallery = new GalleryView { Layout = layout, SelectionMode = GallerySelectionMode.Multiple };

        gallery.Items = Enumerable.Range(0, 14)
            .Select(i => (object)new GalleryItem(Tile(i), captions ? $"Photo {i + 1}" : string.Empty, captions && i % 2 == 0 ? "2026" : null))
            .ToList();

        gallery.SelectedIndex = 2;

        return gallery;
    }

    /// <summary>Draws a colored tile whose shape depends on its number.</summary>
    /// <param name="index">The tile's number.</param>
    static SkiaImage Tile(int index)
    {
        var (width, height) = (index % 4) switch { 0 => (160, 100), 1 => (100, 140), 2 => (120, 120), _ => (180, 100) };
        var color = SKColor.FromHsl(index * 47 % 360, 55, 55);

        return SkiaImage.Create(width, height, canvas => canvas.Clear(color));
    }

    /// <summary>Creates a NavigationView with a header, three destinations and a page.</summary>
    static NavigationView CreateNavigationView()
        => new()
        {
            Items = [new("Home", "home"), new("Library", IsHeader: true), new("Search", "search"), new("Settings", "menu")],
            SelectedIndex = 2,
            Content = new TextBlock { Text = "Search page" },
        };

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

    /// <summary>Gets a small round mask icon, which takes the text color of the Control that shows it.</summary>
    static SkiaImage Dot => dot ??= SkiaImage.Create(
        16,
        16,
        canvas => canvas.DrawCircle(8, 8, 5, new SKPaint { Color = SKColors.Black, IsAntialias = true }),
        asMask: true);

    /// <summary>Creates a TabView with two tabs, so that a selected and an unselected header show.</summary>
    /// <returns>The TabView.</returns>
    static TabView CreateTabView()
    {
        var tabs = new TabView();

        tabs.AddTab("One", new TextBlock { Text = "First" }, Dot);
        tabs.AddTab("Two", new TextBlock { Text = "Second" }, Dot, closable: true);

        return tabs;
    }

    /// <summary>Creates a TextArea with three lines of text.</summary>
    /// <returns>The TextArea.</returns>
    static TextArea CreateTextArea() => new(new TextBuffer("First line\nSecond line\nThird line"));

    /// <summary>Creates a Toolbar with two buttons, a divider and a third button.</summary>
    /// <returns>The Toolbar.</returns>
    static Toolbar CreateToolbar()
    {
        var toolbar = new Toolbar();

        toolbar.AddButton("Cut");
        toolbar.AddButton("Copy");
        toolbar.AddSeparator();
        toolbar.AddButton("Paste");

        return toolbar;
    }

    /// <summary>Creates an open Expander with some content, so that its header and its content show.</summary>
    /// <returns>The Expander.</returns>
    static Expander CreateExpander()
        => new() { Header = "Expander", Content = new TextBlock { Text = "Content" }, IsExpanded = true };

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

        foreach (var category in Categories)
            pages[category] = BuildPage(category);

        selectedCategory = Categories.First();

        Children.Add(pages[selectedCategory]);
    }

    /// <summary>Raised after another category was shown.</summary>
    public event EventHandler? CategoryChanged;

    /// <summary>Gets the names of the categories, in order. Each holds the rows of one group of Controls.</summary>
    public static IEnumerable<string> Categories => Rows.Select(r => r.Category).Distinct();

    /// <summary>Gets the glyph, the short description and the number of Controls of a category, for the list beside the matrix.</summary>
    /// <param name="category">One of <see cref="Categories"/>.</param>
    /// <returns>The facts about the category.</returns>
    public static CategoryInfo Describe(string category)
    {
        var (glyph, description) = Details.GetValueOrDefault(category, ("info", string.Empty));

        return new CategoryInfo(glyph, description, Rows.Count(r => r.Category == category));
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
    public static IEnumerable<string> RowNames => Rows.Select(r => r.Name);

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

        foreach (var row in Rows.Where(r => r.Category == category))
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
