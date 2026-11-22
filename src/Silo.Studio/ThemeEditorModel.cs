using Silo.Styling;
using Silo.Validation;

namespace Silo.Studio;

/// <summary>A property and its value in a Rule, as written in the theme file.</summary>
/// <param name="Property">The property name, or a Token name that starts with <c>--</c>.</param>
/// <param name="Value">The value text.</param>
public sealed record EditorDeclaration(string Property, string Value);

/// <summary>A Rule of the theme file, with what it declares.</summary>
/// <param name="Path">The keys from the root of the document to the Rule, such as <c>["rules", "Button", "&amp;:hover"]</c>.</param>
/// <param name="Selector">The Selector that the Rule has once nesting is resolved, such as <c>Button:hover</c>.</param>
/// <param name="Declarations">The properties and Tokens that the Rule sets, in file order.</param>
public sealed record EditorRule(IReadOnlyList<string> Path, string Selector, IReadOnlyList<EditorDeclaration> Declarations);

/// <summary>
/// The state behind Studio's visual theme editor: a theme file as a <see cref="JsoncDocument"/>, the Rules in it, and the
/// edits that were made, with undo and redo. It has no Controls, so it can be tested and reused by other editors.
/// </summary>
/// <remarks>
/// <para>
/// Every edit goes through the lossless syntax tree, so the file keeps its comments, layout and ordering and only the edited
/// value changes. Documents are immutable, which makes undo and redo a matter of moving between them.
/// </para>
/// <para>
/// <see cref="Issues"/> are the validator's findings on the current text. The theme is checked together with the Stylesheets
/// it is used with, such as the default theme, so Tokens that come from there are known.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var model = new ThemeEditorModel(JsoncDocument.Parse(File.ReadAllText(path), path), [DefaultTheme.Load()], path);
///
/// model.SetDeclaration(model.Rules[0].Path, "background", "#10b981");
/// model.Undo();
/// model.Save();
/// </code>
/// </example>
public sealed class ThemeEditorModel
{
    /// <summary>The Stylesheets that the theme is used together with.</summary>
    readonly IReadOnlyList<Stylesheet> context;

    /// <summary>The document before each edit, newest last.</summary>
    readonly Stack<JsoncDocument> undo = new();

    /// <summary>The documents that undo stepped back from, newest last.</summary>
    readonly Stack<JsoncDocument> redo = new();

    /// <summary>The text of the document at the last save or load.</summary>
    string savedText;

    /// <summary>Creates a model.</summary>
    /// <param name="document">The theme.</param>
    /// <param name="context">The Stylesheets that the theme is used together with, whose Tokens count as declared.</param>
    /// <param name="filePath">Where <see cref="Save"/> writes, or <see langword="null"/> for a theme that has no file.</param>
    public ThemeEditorModel(JsoncDocument document, IReadOnlyList<Stylesheet> context, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        Document = document;
        FilePath = filePath;
        savedText = document.Text;
        this.context = context;
    }


    /// <summary>Raised after the document changed, by an edit, an undo, a redo or a reload.</summary>
    public event EventHandler? Changed;


    /// <summary>Gets the current document.</summary>
    public JsoncDocument Document { get; private set; }

    /// <summary>Gets the file that <see cref="Save"/> writes, or <see langword="null"/>.</summary>
    public string? FilePath { get; }

    /// <summary>Gets a value indicating whether the document differs from what was last saved or loaded.</summary>
    public bool IsDirty => Document.Text != savedText;

    /// <summary>Gets a value indicating whether <see cref="Undo"/> has an edit to take back.</summary>
    public bool CanUndo => undo.Count > 0;

    /// <summary>Gets a value indicating whether <see cref="Redo"/> has an edit to apply again.</summary>
    public bool CanRedo => redo.Count > 0;

    /// <summary>Gets the Rules of the document in file order, with nested Rules after the Rule that holds them.</summary>
    public IReadOnlyList<EditorRule> Rules
    {
        get
        {
            var result = new List<EditorRule>();

            if (Document.Find("rules")?.Value is JsoncObject rules)
                AddRules(result, rules, ["rules"], parentSelector: null);

            return result;
        }
    }

    /// <summary>Gets what the validator finds in the current text.</summary>
    public IReadOnlyList<ValidationIssue> Issues
        => StylesheetValidator.ValidateText(Document.Text, Document.FileName, context);

    /// <summary>Sets the value of a property or Token in a Rule, adding it when the Rule does not set it yet.</summary>
    /// <param name="rulePath">The path of the Rule, from <see cref="EditorRule.Path"/>.</param>
    /// <param name="property">The property or Token name.</param>
    /// <param name="value">The new value text.</param>
    public void SetDeclaration(IReadOnlyList<string> rulePath, string property, string value)
        => Apply(Document.SetValue([.. rulePath, property], value));

    /// <summary>Removes a property or Token from a Rule.</summary>
    /// <param name="rulePath">The path of the Rule.</param>
    /// <param name="property">The property or Token name.</param>
    public void RemoveDeclaration(IReadOnlyList<string> rulePath, string property)
        => Apply(Document.Remove([.. rulePath, property]));

    /// <summary>Adds an empty Rule at the top level of <c>rules</c>.</summary>
    /// <param name="selector">The Selector, which is the key.</param>
    /// <returns>The path of the Rule, which is also the path of an existing Rule with that Selector.</returns>
    public IReadOnlyList<string> AddRule(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);

        string[] path = ["rules", selector];

        if (Document.Find(path) is null)
            Apply(Document.SetRawValue(path, "{}"));

        return path;
    }

    /// <summary>Removes a Rule with everything in it.</summary>
    /// <param name="rulePath">The path of the Rule.</param>
    public void RemoveRule(IReadOnlyList<string> rulePath) => Apply(Document.Remove(rulePath));

    /// <summary>Takes back the last edit.</summary>
    /// <returns><see langword="false"/> when there is nothing to take back.</returns>
    public bool Undo()
    {
        if (undo.Count == 0)
            return false;

        redo.Push(Document);

        Document = undo.Pop();

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Applies the edit that <see cref="Undo"/> took back.</summary>
    /// <returns><see langword="false"/> when there is nothing to apply.</returns>
    public bool Redo()
    {
        if (redo.Count == 0)
            return false;

        undo.Push(Document);

        Document = redo.Pop();

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Writes the document to <see cref="FilePath"/>.</summary>
    /// <exception cref="InvalidOperationException">The model has no file.</exception>
    public void Save()
    {
        File.WriteAllText(FilePath ?? throw new InvalidOperationException("The theme has no file."), Document.Text);

        savedText = Document.Text;
    }

    /// <summary>Replaces the document with text that was changed outside the editor, such as in another editor.</summary>
    /// <param name="text">The new text.</param>
    /// <returns>
    /// <see langword="false"/> when the editor has unsaved edits, which are kept, or when the text is not valid JSONC.
    /// </returns>
    public bool TryReload(string text)
    {
        if (IsDirty || text == Document.Text)
            return false;

        try
        {
            Document = JsoncDocument.Parse(text, Document.FileName);
        }
        catch (StylesheetException)
        {
            return false;
        }

        savedText = text;

        undo.Clear();
        redo.Clear();

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Makes an edited document the current one.</summary>
    /// <param name="edited">The document after the edit.</param>
    void Apply(JsoncDocument edited)
    {
        if (edited.Text == Document.Text)
            return;

        undo.Push(Document);
        redo.Clear();

        Document = edited;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds the Rules in an object, and the Rules nested in them, to a list.</summary>
    /// <param name="result">The list.</param>
    /// <param name="owner">The object whose members are Rules.</param>
    /// <param name="path">The path of <paramref name="owner"/>.</param>
    /// <param name="parentSelector">The Selector of the Rule that holds <paramref name="owner"/>, or <see langword="null"/>.</param>
    static void AddRules(List<EditorRule> result, JsoncObject owner, string[] path, string? parentSelector)
    {
        foreach (var member in owner.Members.Where(m => m.Value is JsoncObject))
        {
            var rule = (JsoncObject)member.Value;
            var selector = Resolve(parentSelector, member.Key);
            var rulePath = (string[])[.. path, member.Key];

            var declarations = rule.Members
                .Where(m => m.Value is JsoncScalar)
                .Select(m => new EditorDeclaration(m.Key, ((JsoncScalar)m.Value).Value))
                .ToList();

            result.Add(new(rulePath, selector, declarations));

            AddRules(result, rule, rulePath, selector);
        }
    }

    /// <summary>Works out the Selector of a nested Rule the way the Stylesheet reader does.</summary>
    /// <param name="parent">The Selector of the Rule that holds it, or <see langword="null"/> at the top level.</param>
    /// <param name="key">The key.</param>
    /// <returns>The Selector.</returns>
    static string Resolve(string? parent, string key)
    {
        if (parent is null)
            return key;

        return key.Contains('&') ? key.Replace("&", parent, StringComparison.Ordinal) : $"{parent} {key}";
    }
}
