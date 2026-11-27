namespace Silo;

/// <summary>
/// How much room a chat control leaves around its content. The theme turns it into spacing through the Classes
/// <c>density-compact</c> and <c>density-roomy</c>.
/// </summary>
public enum ChatDensity
{
    /// <summary>Tight padding and small gaps, for a lot of chat on one screen.</summary>
    Compact,

    /// <summary>The default spacing.</summary>
    Normal,

    /// <summary>More padding and larger gaps.</summary>
    Roomy,
}
