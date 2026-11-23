using System.Globalization;

namespace Silo.Styling.Values;

/// <summary>
/// What the <c>transform</c> property says: a move, a rotation and a scale that are applied to a Control and everything in it
/// when it is painted and hit-tested. Layout does not change.
/// </summary>
/// <param name="TranslateX">The horizontal move in pixels.</param>
/// <param name="TranslateY">The vertical move in pixels.</param>
/// <param name="Rotate">The clockwise rotation in degrees.</param>
/// <param name="ScaleX">The horizontal scale factor, 1 for none.</param>
/// <param name="ScaleY">The vertical scale factor, 1 for none.</param>
/// <remarks>
/// The parts are always applied in the same order, around the transform origin: scale first, then rotate, then move. A
/// Stylesheet can list the functions in any order, and several of the same kind are combined (moves and rotations add, scales
/// multiply). Use <see cref="None"/> and not <see langword="default"/>, whose scale is 0.
/// </remarks>
public readonly record struct Transform(double TranslateX, double TranslateY, double Rotate, double ScaleX, double ScaleY)
{
    /// <summary>Gets the transform that changes nothing.</summary>
    public static Transform None => new(0, 0, 0, 1, 1);

    /// <summary>Gets whether this transform changes nothing.</summary>
    public bool IsNone => this == None;

    /// <summary>Builds the matrix that maps the Control's own coordinates to the coordinates of its parent's space, around an origin.</summary>
    /// <param name="originX">The horizontal origin in pixels, from the left of the border box.</param>
    /// <param name="originY">The vertical origin in pixels, from the top of the border box.</param>
    /// <returns>The matrix.</returns>
    public Matrix2D ToMatrix(double originX, double originY) =>
        Matrix2D.Translation(originX + TranslateX, originY + TranslateY)
            .Multiply(Matrix2D.Rotation(Rotate))
            .Multiply(Matrix2D.Scaling(ScaleX, ScaleY))
            .Multiply(Matrix2D.Translation(-originX, -originY));

    /// <summary>Writes the transform the way a Stylesheet would.</summary>
    /// <returns><c>none</c>, or the functions <c>translate</c>, <c>rotate</c> and <c>scale</c> that differ from their default.</returns>
    public override string ToString()
    {
        if (IsNone)
            return "none";

        var parts = new List<string>();

        if (TranslateX != 0 || TranslateY != 0)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"translate({TranslateX}px, {TranslateY}px)"));

        if (Rotate != 0)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"rotate({Rotate}deg)"));

        if (ScaleX != 1 || ScaleY != 1)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"scale({ScaleX}, {ScaleY})"));

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Tries to read <c>none</c> or a list of the functions <c>translate(x, y)</c>, <c>translateX(x)</c>, <c>translateY(y)</c>,
    /// <c>rotate(angle)</c>, <c>scale(s)</c>, <c>scale(sx, sy)</c>, <c>scaleX(s)</c> and <c>scaleY(s)</c>.
    /// </summary>
    /// <param name="text">The text. Moves are in pixels (a missing <c>px</c> is fine), angles in degrees (<c>deg</c> is optional), scales are plain numbers.</param>
    /// <param name="transform">The transform, or <see cref="None"/> when the text could not be read.</param>
    /// <returns><see langword="false"/> for an unknown function, a wrong number of arguments or a bad number.</returns>
    public static bool TryParse(string text, out Transform transform)
    {
        transform = None;

        var rest = text.Trim();

        if (rest.Length == 0)
            return false;

        if (rest == "none")
            return true;

        var (tx, ty, rotate, sx, sy) = (0.0, 0.0, 0.0, 1.0, 1.0);

        while (rest.Length > 0)
        {
            var open = rest.IndexOf('(');
            var close = rest.IndexOf(')');

            if (open <= 0 || close < open)
                return false;

            var name = rest[..open].Trim();
            var arguments = rest[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);

            rest = rest[(close + 1)..].TrimStart();

            if (!Apply(name, arguments, ref tx, ref ty, ref rotate, ref sx, ref sy))
                return false;
        }

        transform = new Transform(tx, ty, rotate, sx, sy);

        return true;
    }

    /// <summary>Applies one function to the running totals.</summary>
    static bool Apply(string name, string[] arguments, ref double tx, ref double ty, ref double rotate, ref double sx, ref double sy)
    {
        double first, second;

        switch (name)
        {
            case "translate" when arguments.Length is 1 or 2 && Pixels(arguments[0], out first) && (arguments.Length == 1 || Pixels(arguments[1], out second)):
                tx += first;
                ty += arguments.Length == 2 ? Read(arguments[1]) : 0;
                return true;

            case "translateX" when arguments.Length == 1 && Pixels(arguments[0], out first):
                tx += first;
                return true;

            case "translateY" when arguments.Length == 1 && Pixels(arguments[0], out first):
                ty += first;
                return true;

            case "rotate" when arguments.Length == 1 && Degrees(arguments[0], out first):
                rotate += first;
                return true;

            case "scale" when arguments.Length is 1 or 2 && Number(arguments[0], out first) && (arguments.Length == 1 || Number(arguments[1], out second)):
                sx *= first;
                sy *= arguments.Length == 2 ? Read(arguments[1]) : first;
                return true;

            case "scaleX" when arguments.Length == 1 && Number(arguments[0], out first):
                sx *= first;
                return true;

            case "scaleY" when arguments.Length == 1 && Number(arguments[0], out first):
                sy *= first;
                return true;

            default:
                return false;
        }
    }

    /// <summary>Reads a number that was already checked.</summary>
    static double Read(string text) => double.Parse(text.Replace("px", "").Replace("deg", ""), CultureInfo.InvariantCulture);

    /// <summary>Reads a number of pixels.</summary>
    static bool Pixels(string text, out double value) =>
        double.TryParse(text.EndsWith("px", StringComparison.Ordinal) ? text[..^2] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);

    /// <summary>Reads an angle in degrees.</summary>
    static bool Degrees(string text, out double value) =>
        double.TryParse(text.EndsWith("deg", StringComparison.Ordinal) ? text[..^3] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);

    /// <summary>Reads a plain number.</summary>
    static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
