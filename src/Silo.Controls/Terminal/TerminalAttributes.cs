namespace Silo;

/// <summary>The text styles a terminal cell can have, which a program turns on and off with SGR escape sequences.</summary>
[Flags]
public enum TerminalAttributes
{
    /// <summary>No style.</summary>
    None = 0,

    /// <summary>Bold, often drawn as a brighter color.</summary>
    Bold = 1,

    /// <summary>Faint, drawn dimmer.</summary>
    Faint = 2,

    /// <summary>Italic.</summary>
    Italic = 4,

    /// <summary>Underlined.</summary>
    Underline = 8,

    /// <summary>Blinking.</summary>
    Blink = 16,

    /// <summary>Foreground and background swapped.</summary>
    Reverse = 32,

    /// <summary>Not shown.</summary>
    Hidden = 64,

    /// <summary>Struck through.</summary>
    Strikethrough = 128,
}
