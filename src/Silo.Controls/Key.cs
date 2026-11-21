namespace Silo;

/// <summary>The keys that platform windows report to Controls. It grows as Controls need more keys.</summary>
public enum Key
{
    /// <summary>A key that has no value in this enumeration yet.</summary>
    Unknown,

    /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>The F5 key, which tools use for refresh.</summary>
    F5,

    /// <summary>The F12 key, which opens the Inspector.</summary>
    F12,

    /// <summary>The Backspace key.</summary>
    Backspace,

    /// <summary>The Delete key.</summary>
    Delete,

    /// <summary>The left arrow key.</summary>
    Left,

    /// <summary>The right arrow key.</summary>
    Right,

    /// <summary>The Home key.</summary>
    Home,

    /// <summary>The End key.</summary>
    End,

    /// <summary>The Enter or Return key.</summary>
    Enter,
}
