namespace Silo;

/// <summary>How serious a message is, as shown by a <see cref="Toast"/>, an <c>InfoBar</c> or a <c>Badge</c>. The theme colors each kind through its Class.</summary>
public enum Severity
{
    /// <summary>Something to know. The Class is <c>info</c>. This is the default.</summary>
    Info,

    /// <summary>Something that went well. The Class is <c>success</c>.</summary>
    Success,

    /// <summary>Something that needs attention. The Class is <c>warning</c>.</summary>
    Warning,

    /// <summary>Something that went wrong. The Class is <c>error</c>.</summary>
    Error,
}
