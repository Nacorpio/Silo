using System.Globalization;

namespace Silo.Styling.Values;

public enum LengthUnit { Px, Percent, Em, Rem, Fr }

/// <summary>A CSS-style length. A raw number is device-independent pixels.</summary>
public readonly record struct Length(double Value, LengthUnit Unit)
{
    public static bool TryParse(string text, out Length length)
    {
        length = default;

        text = text.Trim();

        var units = new (string Suffix, LengthUnit Unit)[]
        {
            ("px", LengthUnit.Px), ("rem", LengthUnit.Rem), ("em", LengthUnit.Em), ("fr", LengthUnit.Fr), ("%", LengthUnit.Percent),
        };

        foreach (var (suffix, unit) in units)
        {
            if (text.EndsWith(suffix, StringComparison.Ordinal) && Number(text[..^suffix.Length], out var number))
            {
                length = new Length(number, unit);

                return true;
            }
        }

        if (Number(text, out var raw))
        {
            length = new Length(raw, LengthUnit.Px);

            return true;
        }

        return false;
    }

    static bool Number(string text, out double number)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
}
