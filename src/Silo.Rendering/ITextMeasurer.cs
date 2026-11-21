using Silo.Layout;

namespace Silo.Rendering;

/// <summary>
/// Measures text, so that layout can size a TextBlock before anything is drawn.
/// </summary>
/// <remarks>
/// The measurer and the drawing context of a backend must agree: the size returned here is the line box that
/// <see cref="IDrawingContext.DrawText"/> draws into.
/// </remarks>
public interface ITextMeasurer
{
    /// <summary>Measures a single line of text.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="style">The font to measure with.</param>
    /// <returns>The size of the line box: the advance width of the text and the line height of the font.</returns>
    Size Measure(string text, TextStyle style);
}
