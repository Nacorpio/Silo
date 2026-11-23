namespace Silo.Styling.Values;

/// <summary>The point that a <see cref="Transform"/> rotates and scales around, measured from the top left of the border box.</summary>
/// <param name="X">The horizontal position, in pixels or as a percentage of the width.</param>
/// <param name="Y">The vertical position, in pixels or as a percentage of the height.</param>
public readonly record struct TransformOrigin(Length X, Length Y)
{
    /// <summary>Gets the center of the box, <c>50% 50%</c>, which is the default.</summary>
    public static TransformOrigin Center => new(new Length(50, LengthUnit.Percent), new Length(50, LengthUnit.Percent));

    /// <summary>Works out the origin in pixels for a box.</summary>
    /// <param name="width">The width of the border box.</param>
    /// <param name="height">The height of the border box.</param>
    /// <returns>The position, from the top left of the box. Units other than pixels and percent count as pixels.</returns>
    public (double X, double Y) Resolve(double width, double height) => (Resolve(X, width), Resolve(Y, height));

    /// <summary>Writes the origin the way a Stylesheet would.</summary>
    /// <returns>The two lengths separated by a space.</returns>
    public override string ToString() => $"{X} {Y}";

    /// <summary>
    /// Tries to read one or two values, each a length such as <c>10px</c> or <c>25%</c>, or one of the keywords <c>left</c>,
    /// <c>center</c>, <c>right</c>, <c>top</c> and <c>bottom</c>.
    /// </summary>
    /// <param name="text">The text. A single value sets the horizontal position and centers the vertical one. Keywords can come in either order.</param>
    /// <param name="origin">The origin, or <see cref="Center"/> when the text could not be read.</param>
    /// <returns><see langword="false"/> for an unknown word, a bad length or more than two values.</returns>
    public static bool TryParse(string text, out TransformOrigin origin)
    {
        origin = Center;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is 0 or > 2)
            return false;

        if (parts.Length == 1)
        {
            if (parts[0] is "top" or "bottom")
                parts = ["center", parts[0]];
            else
                parts = [parts[0], "center"];
        }

        if (parts[0] is "top" or "bottom" || parts[1] is "left" or "right")
            parts = [parts[1], parts[0]];

        if (!TryPart(parts[0], horizontal: true, out var x) || !TryPart(parts[1], horizontal: false, out var y))
            return false;

        origin = new TransformOrigin(x, y);

        return true;
    }

    /// <summary>Reads one value of the origin.</summary>
    static bool TryPart(string text, bool horizontal, out Length length)
    {
        var percent = text switch
        {
            "left" when horizontal => 0,
            "right" when horizontal => 100,
            "top" when !horizontal => 0,
            "bottom" when !horizontal => 100,
            "center" => 50,

            _ => -1,
        };

        if (percent >= 0)
        {
            length = new Length(percent, LengthUnit.Percent);

            return true;
        }

        return Length.TryParse(text, out length);
    }

    /// <summary>Converts one length to pixels.</summary>
    static double Resolve(Length length, double size) => length.Unit == LengthUnit.Percent ? size * length.Value / 100 : length.Value;
}
