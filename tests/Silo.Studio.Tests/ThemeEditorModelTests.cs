using Silo;
using Silo.Studio;
using Silo.Styling;
using Xunit;

public class ThemeEditorModelTests
{
    const string Source = """
        {
          "tokens": { "--accent": "#3b82f6" },
          "rules": {
            // The primary button.
            "Button": {
              "background": "var(--accent)",
              "&:hover": { "background": "red" },
              ".icon": { "gap": "2px" }
            },
            "TextBlock": { "color": "black" }
          }
        }
        """;

    static ThemeEditorModel Create(string source = Source, string? path = null)
        => new(JsoncDocument.Parse(source, "theme.jsonc"), [DefaultTheme.Load()], path);

    [Fact]
    public void The_Rules_are_listed_in_file_order_with_nested_ones_resolved_like_the_reader_does()
    {
        var model = Create();

        Assert.Equal(["Button", "Button:hover", "Button .icon", "TextBlock"], model.Rules.Select(r => r.Selector));
        Assert.Equal(["rules", "Button", "&:hover"], model.Rules[1].Path);
        Assert.Equal([new EditorDeclaration("background", "var(--accent)")], model.Rules[0].Declarations);
    }

    [Fact]
    public void The_listed_selectors_are_the_ones_the_Stylesheet_has()
    {
        var model = Create();
        var fromReader = model.Document.ToStylesheet().Rules.Select(r => r.Selector.ToString());

        Assert.Equal(fromReader.Order(), model.Rules.Select(r => r.Selector).Order());
    }

    [Fact]
    public void Setting_a_declaration_changes_only_its_value_and_raises_Changed_and_marks_the_model_dirty()
    {
        var model = Create();
        var raised = 0;

        model.Changed += (_, _) => raised++;

        model.SetDeclaration(model.Rules[3].Path, "color", "white");

        Assert.Equal(Source.Replace("\"black\"", "\"white\""), model.Document.Text);
        Assert.Equal(1, raised);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void Setting_the_same_value_again_is_not_an_edit()
    {
        var model = Create();
        var raised = 0;

        model.Changed += (_, _) => raised++;

        model.SetDeclaration(model.Rules[3].Path, "color", "black");

        Assert.Equal(0, raised);
        Assert.False(model.IsDirty);
        Assert.False(model.CanUndo);
    }

    [Fact]
    public void A_new_declaration_is_added_to_the_Rule_and_a_declaration_can_be_removed()
    {
        var model = Create();

        model.SetDeclaration(model.Rules[3].Path, "font-size", "12px");

        Assert.Equal(["color", "font-size"], model.Rules[3].Declarations.Select(d => d.Property));

        model.RemoveDeclaration(model.Rules[3].Path, "color");

        Assert.Equal(["font-size"], model.Rules[3].Declarations.Select(d => d.Property));
    }

    [Fact]
    public void Adding_a_Rule_puts_an_empty_one_at_the_end_and_adding_it_again_changes_nothing()
    {
        var model = Create();
        var path = model.AddRule("Slider");

        Assert.Equal(["rules", "Slider"], path);
        Assert.Equal("Slider", model.Rules[^1].Selector);

        var text = model.Document.Text;

        model.AddRule("Slider");

        Assert.Equal(text, model.Document.Text);
    }

    [Fact]
    public void Removing_a_Rule_removes_it_and_what_is_nested_in_it()
    {
        var model = Create();

        model.RemoveRule(model.Rules[0].Path);

        Assert.Equal(["TextBlock"], model.Rules.Select(r => r.Selector));
    }

    [Fact]
    public void Undo_and_redo_move_between_the_documents_and_a_new_edit_clears_redo()
    {
        var model = Create();

        model.SetDeclaration(model.Rules[3].Path, "color", "white");
        model.SetDeclaration(model.Rules[3].Path, "color", "gray");

        Assert.True(model.Undo());
        Assert.Contains("\"white\"", model.Document.Text);
        Assert.True(model.CanRedo);
        Assert.True(model.Undo());
        Assert.Equal(Source, model.Document.Text);
        Assert.False(model.IsDirty);
        Assert.False(model.Undo());
        Assert.True(model.Redo());

        model.SetDeclaration(model.Rules[3].Path, "color", "blue");

        Assert.False(model.CanRedo);
        Assert.False(model.Redo());
    }

    [Fact]
    public void Issues_follow_the_current_text_and_know_Tokens_from_the_context()
    {
        var model = Create();

        Assert.Empty(model.Issues);

        model.SetDeclaration(model.Rules[3].Path, "colour", "white");

        Assert.Equal("SILO001", Assert.Single(model.Issues).Code);

        model.Undo();

        Assert.Empty(model.Issues);

        model.SetDeclaration(model.Rules[3].Path, "background", "var(--surface)");

        Assert.Empty(model.Issues);
    }

    [Fact]
    public void Saving_writes_the_text_and_clears_the_dirty_flag()
    {
        var path = Path.Combine(Path.GetTempPath(), $"silo-editor-{Guid.NewGuid():N}.jsonc");

        try
        {
            File.WriteAllText(path, Source);

            var model = Create(Source, path);

            model.SetDeclaration(model.Rules[3].Path, "color", "white");
            model.Save();

            Assert.False(model.IsDirty);
            Assert.Equal(model.Document.Text, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Saving_without_a_file_is_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => Create().Save());
    }

    [Fact]
    public void A_reload_replaces_a_clean_document_and_keeps_unsaved_edits_and_ignores_invalid_text()
    {
        var model = Create();
        var raised = 0;

        model.Changed += (_, _) => raised++;

        Assert.True(model.TryReload(Source.Replace("black", "navy")));
        Assert.Contains("navy", model.Document.Text);
        Assert.False(model.IsDirty);
        Assert.False(model.CanUndo);
        Assert.False(model.TryReload("{ broken"));
        Assert.False(model.TryReload(model.Document.Text));

        model.SetDeclaration(model.Rules[3].Path, "color", "white");

        Assert.False(model.TryReload(Source));
        Assert.Contains("\"white\"", model.Document.Text);
        Assert.Equal(2, raised);
    }
}
