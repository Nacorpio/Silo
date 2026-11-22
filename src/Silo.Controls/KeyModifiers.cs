namespace Silo;

/// <summary>The modifier keys that were held down during a key press.</summary>
/// <example>
/// <code language="csharp">
/// var selecting = (modifiers &amp; KeyModifiers.Shift) != 0;
/// </code>
/// </example>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifier key.</summary>
    None = 0,

    /// <summary>A Shift key.</summary>
    Shift = 1,

    /// <summary>A Control key, which on a Mac is the Command key.</summary>
    Control = 2,

    /// <summary>An Alt key, which on a Mac is the Option key.</summary>
    Alt = 4,
}
