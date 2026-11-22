namespace Silo.Studio;

/// <summary>
/// The visual theme editor: the Rules of a theme file in a list, the properties of the selected Rule as editable rows, and
/// buttons for undo, redo and save. Every change goes through a <see cref="ThemeEditorModel"/>, so the file keeps its layout.
/// </summary>
/// <remarks>
/// <para>
/// The panel does not apply the theme. The host listens to <see cref="ThemeEditorModel.Changed"/> and restyles the window, so
/// what a user types shows up in the gallery as they type.
/// </para>
/// <para>
/// While a value is typed into a row, the rows are left alone so the text box keeps its caret. They are built again when
/// another Rule is selected, a property is added or removed, or the model changes in any other way, such as an undo.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var panel = new ThemeEditorPanel(model);
///
/// studioView.Children.Add(panel);
/// </code>
/// </example>
public sealed class ThemeEditorPanel : Box
{
    /// <summary>The model that the panel edits.</summary>
    readonly ThemeEditorModel model;

    /// <summary>The title, with a mark while there are unsaved edits.</summary>
    readonly TextBlock title = new();

    /// <summary>The list of Selectors.</summary>
    readonly ListView rules = new() { Name = "rules" };

    /// <summary>The name of the selected Rule.</summary>
    readonly TextBlock selected = new() { Name = "selected-rule" };

    /// <summary>The rows of the selected Rule.</summary>
    readonly Box declarations = new() { Name = "declarations" };

    /// <summary>The line that tells what the validator found.</summary>
    readonly TextBlock status = new() { Name = "status" };

    /// <summary>The path of the selected Rule, or <see langword="null"/> when none is.</summary>
    IReadOnlyList<string>? selectedPath;

    /// <summary>Whether an edit made in a row is being applied, which keeps the rows from being rebuilt under the caret.</summary>
    bool editingRow;

    /// <summary>Creates the panel for a model.</summary>
    /// <param name="model">The model to edit.</param>
    public ThemeEditorPanel(ThemeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        this.model = model;

        Name = "editor";

        title.AddClass("title");
        selected.AddClass("muted");
        status.AddClass("muted");

        Children.Add(title);
        Children.Add(BuildToolbar());
        Children.Add(rules);
        Children.Add(selected);
        Children.Add(declarations);
        Children.Add(BuildAddRow());
        Children.Add(status);

        rules.SelectionChanged += (_, _) => SelectFromList();
        model.Changed += (_, _) => OnModelChanged();

        Rebuild();
    }

    /// <summary>Gets the path of the selected Rule, or <see langword="null"/>.</summary>
    public IReadOnlyList<string>? SelectedPath => selectedPath;

    /// <summary>Selects a Rule.</summary>
    /// <param name="path">The path of the Rule, from <see cref="EditorRule.Path"/>.</param>
    public void Select(IReadOnlyList<string> path)
    {
        selectedPath = path;

        Rebuild();
    }

    /// <summary>Builds the row of undo, redo and save buttons.</summary>
    /// <returns>The row.</returns>
    Box BuildToolbar()
    {
        var bar = new Box { Name = "editor-toolbar" };
        var undo = new Button { Text = "Undo", Name = "undo" };
        var redo = new Button { Text = "Redo", Name = "redo" };

        undo.Click += (_, _) => model.Undo();
        redo.Click += (_, _) => model.Redo();

        bar.Children.Add(undo);
        bar.Children.Add(redo);

        if (model.FilePath is null)
            return bar;

        var save = new Button { Text = "Save", Name = "save" };

        save.Click += (_, _) => Save();

        bar.Children.Add(save);

        return bar;
    }

    /// <summary>Builds the row where a new property is typed.</summary>
    /// <returns>The row.</returns>
    Box BuildAddRow()
    {
        var row = new Box { Name = "add-row" };
        var property = new TextBox { Placeholder = "property", Name = "new-property" };
        var value = new TextBox { Placeholder = "value", Name = "new-value" };
        var add = new Button { Text = "Add", Name = "add" };

        add.Click += (_, _) => AddDeclaration(property, value);

        row.Children.Add(property);
        row.Children.Add(value);
        row.Children.Add(add);

        return row;
    }

    /// <summary>Adds the typed property to the selected Rule.</summary>
    /// <param name="property">The box with the property name.</param>
    /// <param name="value">The box with the value.</param>
    void AddDeclaration(TextBox property, TextBox value)
    {
        var name = property.Text.Trim();

        if (selectedPath is null || name.Length == 0)
            return;

        model.SetDeclaration(selectedPath, name, value.Text);

        property.Text = string.Empty;
        value.Text = string.Empty;
    }

    /// <summary>Writes the theme file.</summary>
    void Save()
    {
        model.Save();

        Refresh();
    }

    /// <summary>Selects the Rule that the list shows as selected.</summary>
    void SelectFromList()
    {
        var all = model.Rules;

        if (rules.SelectedIndex < 0 || rules.SelectedIndex >= all.Count)
            return;

        var path = all[rules.SelectedIndex].Path;

        if (selectedPath is not null && path.SequenceEqual(selectedPath))
            return;

        selectedPath = path;

        RebuildRows();
    }

    /// <summary>Reacts to a change of the model.</summary>
    void OnModelChanged()
    {
        if (editingRow)
            Refresh();
        else
            Rebuild();
    }

    /// <summary>Updates the list, the rows and the lines of text.</summary>
    void Rebuild()
    {
        var all = model.Rules;

        rules.Items = [.. all.Select(r => r.Selector)];

        if (selectedPath is not null && all.All(r => !r.Path.SequenceEqual(selectedPath)))
            selectedPath = null;

        selectedPath ??= all.FirstOrDefault()?.Path;

        var index = all.ToList().FindIndex(r => selectedPath is not null && r.Path.SequenceEqual(selectedPath));

        rules.SelectedIndex = index;

        RebuildRows();
        Refresh();
    }

    /// <summary>Builds the rows of the selected Rule.</summary>
    void RebuildRows()
    {
        declarations.Children.Clear();

        var rule = model.Rules.FirstOrDefault(r => selectedPath is not null && r.Path.SequenceEqual(selectedPath));

        selected.Text = rule?.Selector ?? "Select a Rule";

        if (rule is null)
            return;

        foreach (var declaration in rule.Declarations)
            declarations.Children.Add(BuildRow(rule.Path, declaration));
    }

    /// <summary>Builds the row of one property.</summary>
    /// <param name="rulePath">The path of its Rule.</param>
    /// <param name="declaration">The property and its value.</param>
    /// <returns>The row.</returns>
    Box BuildRow(IReadOnlyList<string> rulePath, EditorDeclaration declaration)
    {
        var row = new Box();
        var name = new TextBlock { Text = declaration.Property };
        var value = new TextBox { Text = declaration.Value };
        var remove = new Button { Text = "x" };

        row.AddClass("declaration");
        name.AddClass("declaration-name");
        remove.AddClass("remove");

        value.TextChanged += (_, _) => EditRow(rulePath, declaration.Property, value.Text);
        remove.Click += (_, _) => model.RemoveDeclaration(rulePath, declaration.Property);

        row.Children.Add(name);
        row.Children.Add(value);
        row.Children.Add(remove);

        return row;
    }

    /// <summary>Applies a value typed into a row without rebuilding the rows.</summary>
    /// <param name="rulePath">The path of the Rule.</param>
    /// <param name="property">The property.</param>
    /// <param name="text">The new value.</param>
    void EditRow(IReadOnlyList<string> rulePath, string property, string text)
    {
        editingRow = true;

        try
        {
            model.SetDeclaration(rulePath, property, text);
        }
        finally
        {
            editingRow = false;
        }
    }

    /// <summary>Updates the title and the status line.</summary>
    void Refresh()
    {
        var name = model.FilePath is null ? "Theme" : Path.GetFileName(model.FilePath);

        title.Text = model.IsDirty ? $"{name} *" : name;

        var issues = model.Issues;

        status.Text = issues.Count == 0
            ? "No issues"
            : $"{issues.Count} issue(s): {issues[0].Message} (line {issues[0].Span.Line})";
    }
}
