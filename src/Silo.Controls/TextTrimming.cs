using Silo.Rendering;

namespace Silo;

/// <summary>Shortens text that is too wide for the room it has.</summary>
public static class TextTrimming
{
    /// <summary>The character that replaces the end of a trimmed text.</summary>
    const string Ellipsis = "…";

    /// <summary>Cuts a text so that it, with an ellipsis, fits a width.</summary>
    /// <param name="measurer">What the text is measured with.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">How the text is drawn.</param>
    /// <param name="width">The room in pixels.</param>
    /// <returns>The text itself when it fits, otherwise its longest start that fits with an ellipsis, which is just the ellipsis or empty when there is hardly any room.</returns>
    public static string Trim(ITextMeasurer measurer, string text, TextStyle style, double width)
    {
        if (text.Length == 0 || measurer.Measure(text, style).Width <= width)
            return text;

        var low = 0;
        var high = text.Length - 1;

        while (low < high)
        {
            var middle = (low + high + 1) / 2;

            if (measurer.Measure(text[..middle].TrimEnd() + Ellipsis, style).Width <= width)
                low = middle;
            else
                high = middle - 1;
        }

        var result = text[..low].TrimEnd() + Ellipsis;

        return measurer.Measure(result, style).Width <= width ? result : string.Empty;
    }
}
