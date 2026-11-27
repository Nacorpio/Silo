namespace Silo;

/// <summary>
/// How much room a <see cref="ToolCallCard"/> and a <see cref="ToolCallTimeline"/> leave around their content. The theme turns
/// it into spacing through the Classes <c>density-compact</c> and <c>density-roomy</c>.
/// </summary>
public enum ToolCallDensity
{
    /// <summary>Tight rows and small gaps, for many calls on one screen.</summary>
    Compact,

    /// <summary>The default spacing.</summary>
    Normal,

    /// <summary>More padding and larger gaps.</summary>
    Roomy,
}
