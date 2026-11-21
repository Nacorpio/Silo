using Silo.Styling.Values;

namespace Silo.Layout;

/// <summary>
/// Converts Stylesheet lengths to pixels the way layout does, so that other code that needs the same numbers, such as
/// painting a border, agrees with it.
/// </summary>
/// <example>
/// <code language="csharp">
/// double? padding = Lengths.Resolve(new Length(10, LengthUnit.Percent), basis: 200);   // 20
/// </code>
/// </example>
public static class Lengths
{
    /// <summary>The number of pixels that one <c>em</c> or <c>rem</c> resolves to until text metrics are available.</summary>
    public const double EmPixels = 16;

    /// <summary>Resolves an optional length to pixels.</summary>
    /// <param name="length">The length, or <see langword="null"/> for auto.</param>
    /// <param name="basis">The value that 100% stands for, usually the content width of the parent.</param>
    /// <returns>
    /// The size in pixels, or <see langword="null"/> for <see langword="null"/> (auto) and for units that are not valid in
    /// layout, such as <see cref="LengthUnit.Fr"/>. A result can be negative; callers decide whether that is allowed.
    /// </returns>
    public static double? Resolve(Length? length, double basis)
    {
        return length switch
        {
            null => null,
            { Unit: LengthUnit.Px } l => l.Value,
            { Unit: LengthUnit.Percent } l => basis * l.Value / 100,
            { Unit: LengthUnit.Em or LengthUnit.Rem } l => l.Value * EmPixels,

            _ => null,
        };
    }
}
