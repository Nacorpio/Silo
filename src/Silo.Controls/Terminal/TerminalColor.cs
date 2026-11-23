namespace Silo;

/// <summary>What kind of color a <see cref="TerminalColor"/> is.</summary>
public enum TerminalColorKind
{
    /// <summary>The default foreground or background of the terminal.</summary>
    Default,

    /// <summary>An entry of the 256-color palette: 0 to 15 are the theme's colors, 16 to 231 a color cube and 232 to 255 greys.</summary>
    Indexed,

    /// <summary>A true color given as red, green and blue.</summary>
    Rgb,
}

/// <summary>
/// A color of a terminal cell, as a program asked for it: the default, a palette index, or a true color. The terminal control turns
/// it into a real color with the theme's palette.
/// </summary>
/// <example>
/// <code language="csharp">
/// var red = TerminalColor.FromIndex(1);
/// var orange = TerminalColor.FromRgb(255, 165, 0);
/// </code>
/// </example>
public readonly record struct TerminalColor
{
    /// <summary>The default color.</summary>
    public static TerminalColor Default => default;

    /// <summary>Gets the kind of color.</summary>
    public TerminalColorKind Kind { get; private init; }

    /// <summary>Gets the palette index of an <see cref="TerminalColorKind.Indexed"/> color.</summary>
    public byte Index { get; private init; }

    /// <summary>Gets the red part of an <see cref="TerminalColorKind.Rgb"/> color.</summary>
    public byte R { get; private init; }

    /// <summary>Gets the green part of an <see cref="TerminalColorKind.Rgb"/> color.</summary>
    public byte G { get; private init; }

    /// <summary>Gets the blue part of an <see cref="TerminalColorKind.Rgb"/> color.</summary>
    public byte B { get; private init; }

    /// <summary>Creates a color from a palette index.</summary>
    /// <param name="index">The index from 0 to 255.</param>
    public static TerminalColor FromIndex(byte index) => new() { Kind = TerminalColorKind.Indexed, Index = index };

    /// <summary>Creates a true color.</summary>
    /// <param name="r">The red part.</param>
    /// <param name="g">The green part.</param>
    /// <param name="b">The blue part.</param>
    public static TerminalColor FromRgb(byte r, byte g, byte b) => new() { Kind = TerminalColorKind.Rgb, R = r, G = g, B = b };
}
