using SDL;

namespace Silo.Platform.Sdl;

/// <summary>The system clipboard, read and written through SDL.</summary>
/// <remarks>Create it after the SDL video system started, which opening an <see cref="SdlWindow"/> does.</remarks>
/// <example>
/// <code language="csharp">
/// editor.Clipboard = new SdlClipboard();
/// </code>
/// </example>
public sealed class SdlClipboard : IClipboard
{
    /// <inheritdoc/>
    public string GetText()
    {
        var text = SDL3.SDL_GetClipboardText();

        return text ?? string.Empty;
    }

    /// <inheritdoc/>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        SDL3.SDL_SetClipboardText(text);
    }
}
