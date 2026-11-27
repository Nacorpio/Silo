namespace Silo;

/// <summary>
/// The text area of a <see cref="ChatInput"/>. It lets the input decide what a key does before the editor sees it, so that Enter
/// can send instead of breaking the line.
/// </summary>
/// <remarks>It reports <c>TextArea</c> as its type name, so the theme's TextArea Rules style it and a <c>.composer</c> Rule refines them.</remarks>
/// <example>
/// <code language="csharp">
/// var area = new ComposerArea { Intercept = (key, modifiers) => key == Key.Enter };
/// </code>
/// </example>
public sealed class ComposerArea : TextArea
{
    /// <summary>Gets <c>TextArea</c>, so that the theme's TextArea Rules apply.</summary>
    public override string TypeName => "TextArea";

    /// <summary>Gets or sets a function that sees each key first. When it returns <see langword="true"/> the key is used up.</summary>
    public Func<Key, KeyModifiers, bool>? Intercept { get; set; }

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
        => Intercept?.Invoke(key, modifiers) == true || base.KeyDown(key, modifiers);

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);
}
