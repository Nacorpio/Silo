using Silo.Platform.Sdl;

namespace Silo.Inspector;

/// <summary>Extensions that make an <see cref="SdlWindow"/> inspectable.</summary>
public static class SdlWindowExtensions
{
    /// <summary>Serves the window's Element tree to the Inspector, which opens in a second window on F12.</summary>
    /// <param name="window">The window to inspect.</param>
    /// <param name="pipeName">The pipe name, or <see langword="null"/> for <c>silo-diagnostics-</c> followed by the process id.</param>
    /// <returns>The attachment. Dispose it to stop serving.</returns>
    public static DiagnosticsAttachment AttachDiagnostics(this SdlWindow window, string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        return new DiagnosticsAttachment(window, pipeName);
    }
}
