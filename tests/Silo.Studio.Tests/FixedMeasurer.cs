using Silo.Layout;
using Silo.Rendering;

/// <summary>Measures every character as 7 pixels wide and every line as 14 pixels high, whatever the font.</summary>
public class FixedMeasurer : ITextMeasurer
{
    public Size Measure(string text, TextStyle style) => new(text.Length * 7, 14);
}
