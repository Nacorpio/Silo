using Silo;
using Silo.Studio;
using Silo.Styling;
using Xunit;

public class ThemeEditorPanelTests
{
    const string Source = """
        {
          "rules": {
            "Button": { "background": "red", "padding": "4px" },
            "TextBlock": { "color": "black" }
          }
        }
        """;

    static (ThemeEditorModel Model, ThemeEditorPanel Panel) Create()
    {
        var model = new ThemeEditorModel(JsoncDocument.Parse(Source, "theme.jsonc"), [DefaultTheme.Load()]);

        return (model, new ThemeEditorPanel(model));
    }

    static T Find<T>(Control root, string name) where T : Control
        => (T?)FindOrNull(root, name) ?? throw new InvalidOperationException(name);

    static Control? FindOrNull(Control root, string name)
    {
        if (root.Name == name)
            return root;

        foreach (var child in root.Children)
            if (FindOrNull((Control)child, name) is { } found)
                return found;

        return null;
    }

    static List<TextBox> RowBoxes(ThemeEditorPanel panel)
        => [.. Find<Box>(panel, "declarations").Children.Cast<Control>().Select(r => (TextBox)r.Children[1])];

    static void Click(Button button)
    {
        button.PointerDown();
        button.PointerUp(inside: true);
    }

    [Fact]
    public void The_first_Rule_is_selected_and_its_properties_are_shown_as_rows()
    {
        var (_, panel) = Create();

        Assert.Equal(["rules", "Button"], panel.SelectedPath);
        Assert.Equal(["red", "4px"], RowBoxes(panel).Select(b => b.Text));
        Assert.Equal(["Button", "TextBlock"], Find<ListView>(panel, "rules").Items);
    }

    [Fact]
    public void Selecting_another_Rule_in_the_list_shows_its_rows()
    {
        var (_, panel) = Create();

        Find<ListView>(panel, "rules").SelectedIndex = 1;

        Assert.Equal(["rules", "TextBlock"], panel.SelectedPath);
        Assert.Equal(["black"], RowBoxes(panel).Select(b => b.Text));
        Assert.Equal("TextBlock", Find<TextBlock>(panel, "selected-rule").Text);
    }

    [Fact]
    public void Typing_in_a_row_edits_the_file_and_keeps_the_same_text_box()
    {
        var (model, panel) = Create();
        var box = RowBoxes(panel)[0];

        box.Text = "blue";

        Assert.Contains("\"background\": \"blue\"", model.Document.Text);
        Assert.Same(box, RowBoxes(panel)[0]);
        Assert.Equal("Theme *", ((TextBlock)panel.Children[0]).Text);
    }

    [Fact]
    public void The_Add_button_adds_the_typed_property_to_the_selected_Rule_and_clears_the_boxes()
    {
        var (model, panel) = Create();

        Find<TextBox>(panel, "new-property").Text = "gap";
        Find<TextBox>(panel, "new-value").Text = "8px";

        Click(Find<Button>(panel, "add"));

        Assert.Equal(["background", "padding", "gap"], model.Rules[0].Declarations.Select(d => d.Property));
        Assert.Equal(["red", "4px", "8px"], RowBoxes(panel).Select(b => b.Text));
        Assert.Equal(string.Empty, Find<TextBox>(panel, "new-property").Text);
    }

    [Fact]
    public void Add_does_nothing_without_a_property_name()
    {
        var (model, panel) = Create();

        Find<TextBox>(panel, "new-value").Text = "8px";

        Click(Find<Button>(panel, "add"));

        Assert.False(model.IsDirty);
    }

    [Fact]
    public void The_remove_button_of_a_row_removes_the_property()
    {
        var (model, panel) = Create();
        var remove = (Button)Find<Box>(panel, "declarations").Children.Cast<Control>().First().Children[2];

        Click(remove);

        Assert.Equal(["padding"], model.Rules[0].Declarations.Select(d => d.Property));
        Assert.Equal(["4px"], RowBoxes(panel).Select(b => b.Text));
    }

    [Fact]
    public void Undo_and_redo_buttons_rebuild_the_rows()
    {
        var (_, panel) = Create();

        RowBoxes(panel)[0].Text = "blue";

        Click(Find<Button>(panel, "undo"));

        Assert.Equal("red", RowBoxes(panel)[0].Text);

        Click(Find<Button>(panel, "redo"));

        Assert.Equal("blue", RowBoxes(panel)[0].Text);
    }

    [Fact]
    public void The_status_line_shows_the_first_issue_and_goes_back_to_no_issues()
    {
        var (_, panel) = Create();
        var status = Find<TextBlock>(panel, "status");

        Assert.Equal("No issues", status.Text);

        RowBoxes(panel)[1].Text = "wide";

        Assert.StartsWith("1 issue(s): 'wide' is not a valid value for 'padding'", status.Text);

        RowBoxes(panel)[1].Text = "4px";

        Assert.Equal("No issues", status.Text);
    }

    [Fact]
    public void Save_is_only_offered_for_a_theme_that_has_a_file()
    {
        var (_, panel) = Create();

        Assert.Null(FindOrNull(panel, "save"));

        var path = Path.Combine(Path.GetTempPath(), $"silo-panel-{Guid.NewGuid():N}.jsonc");

        try
        {
            File.WriteAllText(path, Source);

            var model = new ThemeEditorModel(JsoncDocument.Parse(Source, path), [DefaultTheme.Load()], path);
            var withFile = new ThemeEditorPanel(model);

            RowBoxes(withFile)[0].Text = "blue";

            Click(Find<Button>(withFile, "save"));

            Assert.False(model.IsDirty);
            Assert.Contains("blue", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_Studio_view_shows_the_editor_only_when_it_has_a_theme_to_edit()
    {
        var (model, _) = Create();

        Assert.Null(new StudioView().Editor);
        Assert.NotNull(new StudioView(model).Editor);
        Assert.True(new StudioView(model).InitialWidth > new StudioView().InitialWidth);
    }
}
