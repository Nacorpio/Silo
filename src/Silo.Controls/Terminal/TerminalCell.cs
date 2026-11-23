using System.Text;

namespace Silo;

/// <summary>One character position of a terminal screen: the character and how it is drawn.</summary>
/// <param name="Character">The character. A blank cell holds a space.</param>
/// <param name="Foreground">The text color.</param>
/// <param name="Background">The color behind the text.</param>
/// <param name="Attributes">The text styles.</param>
public readonly record struct TerminalCell(
    Rune Character,
    TerminalColor Foreground = default,
    TerminalColor Background = default,
    TerminalAttributes Attributes = TerminalAttributes.None)
{
    /// <summary>Gets a blank cell with the default colors.</summary>
    public static TerminalCell Blank => new(new Rune(' '));

    /// <summary>Gets whether the cell holds a space.</summary>
    public bool IsBlank => Character.Value == ' ';
}
