namespace Silo.Styling.Values;

/// <summary>
/// The four side values of a margin, a padding or a border width, one <see cref="Length"/> for each side.
/// </summary>
/// <param name="Top">The length on the top side.</param>
/// <param name="Right">The length on the right side.</param>
/// <param name="Bottom">The length on the bottom side.</param>
/// <param name="Left">The length on the left side.</param>
/// <remarks>
/// The sides are named in the order that the CSS shorthand writes them, clockwise from the top, and
/// <see cref="TryParse"/> expands the one to four values of that shorthand into all four sides.
/// </remarks>
public readonly record struct Thickness(Length Top, Length Right, Length Bottom, Length Left)
{
    /// <summary>Writes the four sides in the shortest CSS shorthand that means the same, such as <c>8px</c>, <c>8px 16px</c> or <c>1px 2px 3px 4px</c>.</summary>
    /// <returns>The shorthand text.</returns>
    public override string ToString()
    {
        if (Top == Right && Top == Bottom && Top == Left)
        {
            return Top.ToString();
        }

        if (Top == Bottom && Left == Right)
        {
            return $"{Top} {Right}";
        }

        return $"{Top} {Right} {Bottom} {Left}";
    }

    /// <summary>
    /// Tries to read the CSS shorthand for four sides, such as <c>8</c>, <c>8 16</c>, <c>1 2 3</c> or <c>1 2 3 4</c>.
    /// Nothing is thrown for bad input.
    /// </summary>
    /// <param name="text">
    /// One to four lengths separated by white space, each in a form that <see cref="Length.TryParse"/> accepts. They are
    /// expanded as in CSS: one value is used for all four sides; two values are the top and bottom, then the left and right;
    /// three values are the top, then the left and right, then the bottom; four values are the top, right, bottom and left.
    /// </param>
    /// <param name="thickness">
    /// When the method returns <see langword="true"/>, the four sides; otherwise <see langword="default"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there are one to four values and every one is a valid length; <see langword="false"/> for an
    /// empty string, more than four values, or any value that is not a length.
    /// </returns>
    /// <example>
    /// <code language="csharp">
    /// Thickness.TryParse("8", out var all);          // 8 on every side
    /// Thickness.TryParse("8 16", out var padding);   // top and bottom 8, left and right 16
    /// Thickness.TryParse("1 2 3 4", out var each);   // top 1, right 2, bottom 3, left 4
    ///
    /// Thickness.TryParse("1 2 3 4 5", out _);        // false: more than four values
    /// </code>
    /// </example>
    public static bool TryParse(string text, out Thickness thickness)
    {
        thickness = default;

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var lengths = new Length[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!Length.TryParse(parts[i], out lengths[i]))
            {
                return false;
            }
        }

        thickness = lengths.Length switch
        {
            1 => new Thickness(lengths[0], lengths[0], lengths[0], lengths[0]),
            2 => new Thickness(lengths[0], lengths[1], lengths[0], lengths[1]),
            3 => new Thickness(lengths[0], lengths[1], lengths[2], lengths[1]),
            _ => new Thickness(lengths[0], lengths[1], lengths[2], lengths[3]),
        };

        return true;
    }
}
