namespace Silo;

/// <summary>
/// A single line of text that is drawn as dots, for entering a password.
/// </summary>
/// <remarks>
/// <para>
/// The PasswordBox is a <see cref="TextBox"/> that draws <see cref="MaskCharacter"/> for each character, so <see cref="TextBox.Text"/>
/// still holds the real text. Set <see cref="IsRevealed"/> to draw the real text, for a "show password" toggle.
/// </para>
/// <para>Copy is not offered yet, since the TextBox has no selection. Paste arrives with it.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var password = new PasswordBox { Placeholder = "Password" };
/// var show = new CheckBox { Text = "Show" };
///
/// show.CheckedChanged += (_, _) => password.IsRevealed = show.IsChecked;
/// </code>
/// </example>
public class PasswordBox : TextBox
{
    /// <summary>Gets or sets the character that is drawn for each character of the text. The default is a bullet.</summary>
    public char MaskCharacter { get; set; } = '•';

    /// <summary>Gets or sets whether the real text is drawn instead of the dots. The default is <see langword="false"/>.</summary>
    public bool IsRevealed { get; set; }

    /// <inheritdoc/>
    protected override string Display(string value) => IsRevealed ? value : new string(MaskCharacter, value.Length);
}
