namespace Silo;

/// <summary>A row of removable chips followed by a text box in which the user types the next one.</summary>
/// <remarks>
/// <para>
/// Enter or a comma turns the typed text into a token. Backspace in the empty text box removes the last token, and the × button
/// of a chip removes that chip. Blank text is ignored, and so is a token that is already there unless
/// <see cref="AllowDuplicates"/> is set.
/// </para>
/// <para>
/// With <see cref="Suggestions"/> set, Enter turns the typed text into the only suggestion that starts with it (ignoring case),
/// if there is exactly one, otherwise into the text as typed. Set <see cref="RestrictToSuggestions"/> to drop text that matches no
/// suggestion.
/// </para>
/// <para>
/// A chip has the Class <c>token</c> with the parts <c>token-text</c> and <c>token-remove</c>, and the text box has the Class
/// <c>entry</c>. The chips do not wrap: the row is one line, so give the TokenBox a <c>width</c>. Apply styles again after the
/// tokens change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tags = new TokenBox { Suggestions = ["bug", "feature", "docs"] };
///
/// tags.Add("bug");
/// tags.TokensChanged += (_, _) => issue.Labels = tags.Tokens;
/// </code>
/// </example>
[PseudoStates("focus")]
public class TokenBox : Control
{
    /// <summary>The text box for the next token.</summary>
    readonly EntryBox entry = new();

    /// <summary>The tokens.</summary>
    readonly List<string> tokens = [];


    /// <summary>Whether the text box is being cleared by the TokenBox itself.</summary>
    bool suppress;


    /// <summary>
    /// Runs before the first TokenBox is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TokenBox()
    {
    }

    /// <summary>Creates an empty TokenBox.</summary>
    public TokenBox()
    {
        entry.AddClass("entry");

        entry.Submitted += (_, _) => Commit();
        entry.TextChanged += (_, _) =>
        {
            if (entry.Text.EndsWith(',') && !suppress)
                Commit();
        };
        entry.BackspaceOnEmpty += (_, _) =>
        {
            if (tokens.Count > 0)
                RemoveAt(tokens.Count - 1);
        };

        Rebuild();
    }

    /// <summary>Raised after a token was added or removed.</summary>
    public event EventHandler? TokensChanged;

    /// <summary>Gets the tokens in order.</summary>
    public IReadOnlyList<string> Tokens => tokens;

    /// <summary>Gets or sets the texts that Enter completes to. See the remarks.</summary>
    public IReadOnlyList<string> Suggestions { get; set; } = [];

    /// <summary>Gets or sets whether text that is not a suggestion is dropped. The default is <see langword="false"/>.</summary>
    public bool RestrictToSuggestions { get; set; }

    /// <summary>Gets or sets whether the same token can be there twice. The default is <see langword="false"/>.</summary>
    public bool AllowDuplicates { get; set; }

    /// <summary>Gets the text box in which the next token is typed, so that code can set its placeholder.</summary>
    public TextBox Entry => entry;

    /// <summary>Adds a token.</summary>
    /// <param name="token">The text. Blank text, and a duplicate unless <see cref="AllowDuplicates"/> is set, is ignored.</param>
    /// <returns><see langword="true"/> when the token was added.</returns>
    public bool Add(string token)
    {
        var text = token?.Trim() ?? string.Empty;

        if (text.Length == 0 || (!AllowDuplicates && tokens.Contains(text, StringComparer.OrdinalIgnoreCase)))
            return false;

        tokens.Add(text);

        Rebuild();

        TokensChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Removes a token.</summary>
    /// <param name="index">The index of the token. A value outside the list does nothing.</param>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= tokens.Count)
            return;

        tokens.RemoveAt(index);

        Rebuild();

        TokensChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes every token.</summary>
    public void Clear()
    {
        if (tokens.Count == 0)
            return;

        tokens.Clear();

        Rebuild();

        TokensChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Turns the typed text into a token and clears the text box.</summary>
    void Commit()
    {
        var text = entry.Text.Trim().TrimEnd(',').Trim();
        var matches = Suggestions.Where(s => s.StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToList();

        if (text.Length > 0 && matches.Count == 1)
            text = matches[0];

        suppress = true;
        entry.Text = string.Empty;
        suppress = false;

        if (RestrictToSuggestions && !Suggestions.Contains(text, StringComparer.OrdinalIgnoreCase))
            return;

        Add(text);
    }

    /// <summary>Builds the chips and puts the text box after them.</summary>
    void Rebuild()
    {
        Children.Clear();

        for (var i = 0; i < tokens.Count; i++)
        {
            var chip = new Box();
            var label = new TextBlock { Text = tokens[i] };
            var remove = new Button { Text = "×" };
            var index = i;

            chip.AddClass("token");
            label.AddClass("token-text");
            remove.AddClass("token-remove");

            remove.Click += (_, _) => RemoveAt(index);

            chip.Children.Add(label);
            chip.Children.Add(remove);
            Children.Add(chip);
        }

        Children.Add(entry);
    }

    /// <summary>The text box, which reports Backspace in an empty text.</summary>
    sealed class EntryBox : TextBox
    {
        /// <summary>Raised when Backspace is pressed while there is no text.</summary>
        public event EventHandler? BackspaceOnEmpty;

        /// <summary>Gets the type name that Selectors see, which is that of a plain TextBox so that it takes the same styles.</summary>
        public override string TypeName => "TextBox";

        /// <inheritdoc/>
        public override bool KeyDown(Key key)
        {
            if (key != Key.Backspace || Text.Length > 0)
                return base.KeyDown(key);

            BackspaceOnEmpty?.Invoke(this, EventArgs.Empty);

            return true;
        }
    }
}
