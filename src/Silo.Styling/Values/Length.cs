using System.Globalization;

namespace Silo.Styling.Values;

/// <summary>
/// The unit of a <see cref="Length"/>. A unit says what the number is measured against; converting it to device-independent
/// pixels happens later, when the Computed and Used values are worked out.
/// </summary>
public enum LengthUnit
{
    /// <summary>Device-independent pixels, written <c>px</c>. This is also the unit of a number written without one.</summary>
    Px,

    /// <summary>A percentage of a reference size, written <c>%</c>. What the size is depends on the property that uses it, for example the width of the containing box.</summary>
    Percent,

    /// <summary>A multiple of the Element's own computed font size, written <c>em</c>.</summary>
    Em,

    /// <summary>A multiple of the font size of the root Element, written <c>rem</c>.</summary>
    Rem,

    /// <summary>A fraction of the free space that a grid or flex layout shares out between tracks, written <c>fr</c>.</summary>
    Fr,
}

/// <summary>
/// A CSS-style length: a number together with the unit it is measured in. A number written without a unit is in
/// device-independent pixels.
/// </summary>
/// <param name="Value">The number. It may be negative or fractional.</param>
/// <param name="Unit">The unit that <paramref name="Value"/> is measured in.</param>
/// <remarks>
/// A <see cref="Length"/> only records what a Stylesheet said. It does not convert between units, because converting
/// <see cref="LengthUnit.Em"/>, <see cref="LengthUnit.Rem"/> and <see cref="LengthUnit.Percent"/> needs the font size or the
/// layout, which only the later stages of the Cascade have.
/// </remarks>
public readonly record struct Length(double Value, LengthUnit Unit)
{
    /// <summary>
    /// Tries to read a length such as <c>12</c>, <c>12px</c>, <c>50%</c>, <c>1.5em</c>, <c>2rem</c> or <c>1fr</c>.
    /// Nothing is thrown for bad input.
    /// </summary>
    /// <param name="text">
    /// A number, optionally followed directly by one of the unit suffixes <c>px</c>, <c>%</c>, <c>em</c>, <c>rem</c> or
    /// <c>fr</c>. Suffixes are lower case, and leading and trailing white space is ignored. A number without a suffix is in
    /// pixels. The number may have a sign and a fraction and is read with the invariant culture.
    /// </param>
    /// <param name="length">
    /// When the method returns <see langword="true"/>, the parsed length; otherwise <see langword="default"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="text"/> is a length; <see langword="false"/> for an empty string, a unit
    /// without a number, an unsupported unit such as <c>pt</c>, a value that is not finite, or a function such as
    /// <c>calc()</c>, which Stylesheets do not support in v1.
    /// </returns>
    /// <example>
    /// <code language="csharp">
    /// Length.TryParse("12", out var a);      // true: Length(12, LengthUnit.Px)
    /// Length.TryParse("50%", out var b);     // true: Length(50, LengthUnit.Percent)
    /// Length.TryParse("1.5em", out var c);   // true: Length(1.5, LengthUnit.Em)
    ///
    /// Length.TryParse("12pt", out _);        // false: unsupported unit
    /// </code>
    /// </example>
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

    /// <summary>Reads a finite number with the invariant culture.</summary>
    static bool Number(string text, out double number)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
}
