namespace Silo.Studio;

/// <summary>
/// The theme editor: the Rules of a theme file in a list, the properties of the selected Rule as editable rows, or the text of
/// the file in a code editor, and buttons for undo, redo and save. Every change goes through a <see cref="ThemeEditorModel"/>,
/// so the file keeps its layout.
/// </summary>
/// <remarks>
/// <para>
/// The panel does not apply the theme. The host listens to <see cref="ThemeEditorModel.Changed"/> and restyles the window, so
/// what a user types shows up in the gallery as they type.
/// </para>
/// <para>
/// The Visual and Text buttons switch between the two views of the same document. An edit in one shows in the other. Text
/// that is not valid JSONC is not applied: the status line says what is wrong and the theme keeps its last valid state, and
/// Save waits until the text reads again.
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

    /// <summary>The view with the list of Rules and the rows of the selected Rule.</summary>
    readonly Box visual = new() { Name = "visual-view" };

    /// <summary>The view with the text of the file.</summary>
    readonly CodeEditor source = new() { Name = "source" };

    /// <summary>Holds the view that is shown.</summary>
    readonly Box content = new() { Name = "editor-content" };

    /// <summary>The button that shows the visual view.</summary>
    readonly Button visualButton = new() { Text = "Visual", Name = "mode-visual" };

    /// <summary>The button that shows the text view.</summary>
    readonly Button textButton = new() { Text = "Text", Name = "mode-text" };

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

    /// <summary>Whether text typed in the code editor is being applied, which keeps the text from being set under the caret.</summary>
    bool editingText;

    /// <summary>Whether the text view is shown.</summary>
    bool textMode;

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

        visual.Children.Add(rules);
        visual.Children.Add(selected);
        visual.Children.Add(declarations);
        visual.Children.Add(BuildAddRow());
        visual.Children.Add(BuildAddRuleRow());

        content.Children.Add(visual);

        Children.Add(title);
        Children.Add(BuildToolbar());
        Children.Add(BuildModes());
        Children.Add(content);
        Children.Add(status);

        rules.SelectionChanged += (_, _) => SelectFromList();
        source.TextChanged += (_, _) => EditText();
        model.Changed += (_, _) => OnModelChanged();

        source.Buffer.Reset(model.Document.Text);

        Rebuild();
        ShowMode();
    }

    /// <summary>Gets or sets whether the text of the file is shown instead of the list of Rules. The default is <see langword="false"/>.</summary>
    public bool TextMode
    {
        get => textMode;
        set
        {
            if (textMode == value)
                return;

            textMode = value;

            ShowMode();
        }
    }

    /// <summary>Gets the code editor of the text view.</summary>
    public CodeEditor Source => source;

    /// <summary>Gets or sets what copy, cut and paste use in the text view.</summary>
    public IClipboard? Clipboard
    {
        get => source.Clipboard;
        set => source.Clipboard = value;
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

    /// <summary>Builds the row of buttons that switch between the views.</summary>
    /// <returns>The row.</returns>
    Box BuildModes()
    {
        var bar = new Box { Name = "editor-modes" };

        visualButton.Click += (_, _) => TextMode = false;
        textButton.Click += (_, _) => TextMode = true;

        bar.Children.Add(visualButton);
        bar.Children.Add(textButton);

        return bar;
    }

    /// <summary>Shows the view of the current mode and marks its button.</summary>
    void ShowMode()
    {
        content.Children.Clear();
        content.Children.Add(textMode ? source : visual);

        _ = textMode ? textButton.AddClass("selected") : textButton.RemoveClass("selected");
        _ = textMode ? visualButton.RemoveClass("selected") : visualButton.AddClass("selected");
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

    /// <summary>Builds the row where a new Rule is typed.</summary>
    /// <returns>The row.</returns>
    Box BuildAddRuleRow()
    {
        var row = new Box { Name = "add-rule-row" };
        var selector = new TextBox { Placeholder = "new Rule, such as Slider", Name = "new-rule" };
        var add = new Button { Text = "Add rule", Name = "add-rule" };

        add.Click += (_, _) => AddRule(selector);

        row.Children.Add(selector);
        row.Children.Add(add);

        return row;
    }

    /// <summary>Adds the typed Rule and selects it.</summary>
    /// <param name="selector">The box with the Selector.</param>
    void AddRule(TextBox selector)
    {
        var text = selector.Text.Trim();

        if (text.Length == 0)
            return;

        selector.Text = string.Empty;
        selectedPath = model.AddRule(text);

        Rebuild();
    }

    /// <summary>Selects the Rule that styles a Control type, such as when its row in the gallery is clicked.</summary>
    /// <param name="type">The type name.</param>
    /// <returns><see langword="false"/> when the theme has no Rule for the type.</returns>
    public bool SelectType(string type)
    {
        var rule = model.FindRuleForType(type);

        if (rule is null)
            return false;

        Select(rule.Path);

        return true;
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
        if (model.TextError is null)
            model.Save();

        Refresh();
    }

    /// <summary>Selects the Rule that the list shows as selected.</summary>
    void SelectFromList()
    {
        var all = model.Targets;

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
        if (editingRow || editingText)
        {
            Refresh();

            return;
        }

        if (source.Buffer.ToFileText() != model.Document.Text)
            source.Buffer.Reset(model.Document.Text);

        Rebuild();
    }

    /// <summary>Applies the text that was typed in the code editor to the model.</summary>
    void EditText()
    {
        editingText = true;

        try
        {
            model.SetText(source.Buffer.ToFileText());
        }
        finally
        {
            editingText = false;
        }

        Refresh();
    }

    /// <summary>Updates the list, the rows and the lines of text.</summary>
    void Rebuild()
    {
        var all = model.Targets;

        rules.Items = [.. all.Select(r => r.Selector)];

        if (selectedPath is not null && all.All(r => !r.Path.SequenceEqual(selectedPath)))
            selectedPath = null;

        selectedPath ??= (all.Count > 1 ? all[1] : all[0]).Path;

        var index = all.ToList().FindIndex(r => selectedPath is not null && r.Path.SequenceEqual(selectedPath));

        rules.SelectedIndex = index;

        RebuildRows();
        Refresh();
    }

    /// <summary>Builds the rows of the selected Rule.</summary>
    void RebuildRows()
    {
        declarations.Children.Clear();

        var rule = model.Targets.FirstOrDefault(r => selectedPath is not null && r.Path.SequenceEqual(selectedPath));

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

        if (model.TextError is not null)
        {
            status.Text = $"Syntax error: {model.TextError}";

            return;
        }

        var issues = model.Issues;

        status.Text = issues.Count == 0
            ? "No issues"
            : $"{issues.Count} issue(s): {issues[0].Message} (line {issues[0].Span.Line})";
    }
}
