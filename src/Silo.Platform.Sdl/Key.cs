namespace Silo.Platform.Sdl;

/// <summary>The keys that <see cref="SdlWindow.KeyPressed"/> reports. It grows as Controls need more keys.</summary>
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
}
