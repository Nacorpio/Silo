namespace Silo.Studio;

/// <summary>Reads and writes the text that the clipboard holds.</summary>
public interface IClipboard
{
    /// <summary>Gets the text on the clipboard.</summary>
    /// <returns>The text, or an empty string when the clipboard holds no text.</returns>
    string GetText();

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    void SetText(string text);
}
