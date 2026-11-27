using System.Globalization;

namespace Silo.Styling.Values;

/// <summary>
/// A timing curve that turns the fraction of a transition's time that has passed into the fraction of the change that is shown. The
/// built-in curves are the ones that CSS has: <c>linear</c>, <c>ease</c>, <c>ease-in</c>, <c>ease-out</c>, <c>ease-in-out</c>,
/// <c>cubic-bezier(x1, y1, x2, y2)</c> and <c>steps(n)</c>.
/// </summary>
public abstract class Easing
{
    /// <summary>Gets the curve that changes evenly.</summary>
    public static Easing Linear { get; } = new LinearEasing();

    /// <summary>Gets the default curve, which starts gently, speeds up and ends gently: <c>cubic-bezier(0.25, 0.1, 0.25, 1)</c>.</summary>
    public static Easing Ease { get; } = new CubicBezierEasing(0.25, 0.1, 0.25, 1, "ease");

    /// <summary>Gets the name of the curve, which is also what a Stylesheet would write.</summary>
    /// <returns>The text.</returns>
    public abstract override string ToString();

    /// <summary>Gets the curve that starts slowly: <c>cubic-bezier(0.42, 0, 1, 1)</c>.</summary>
    public static Easing EaseIn { get; } = new CubicBezierEasing(0.42, 0, 1, 1, "ease-in");

    /// <summary>Gets the curve that ends slowly: <c>cubic-bezier(0, 0, 0.58, 1)</c>.</summary>
    public static Easing EaseOut { get; } = new CubicBezierEasing(0, 0, 0.58, 1, "ease-out");

    /// <summary>Gets the curve that starts and ends slowly: <c>cubic-bezier(0.42, 0, 0.58, 1)</c>.</summary>
    public static Easing EaseInOut { get; } = new CubicBezierEasing(0.42, 0, 0.58, 1, "ease-in-out");

    /// <summary>Creates a curve from two control points, the way <c>cubic-bezier</c> does.</summary>
    /// <param name="x1">The horizontal position of the first point, from 0 to 1.</param>
    /// <param name="y1">The vertical position of the first point. It may leave 0..1, which makes the change overshoot.</param>
    /// <param name="x2">The horizontal position of the second point, from 0 to 1.</param>
    /// <param name="y2">The vertical position of the second point.</param>
    /// <returns>The curve.</returns>
    public static Easing CubicBezier(double x1, double y1, double x2, double y2) =>
        new CubicBezierEasing(Math.Clamp(x1, 0, 1), y1, Math.Clamp(x2, 0, 1), y2);

    /// <summary>Creates a curve that jumps in equal steps.</summary>
    /// <param name="count">The number of steps, at least 1.</param>
    /// <param name="jumpStart"><see langword="true"/> to jump at the start of each step, <see langword="false"/> (the default) to jump at its end.</param>
    /// <returns>The curve.</returns>
    public static Easing Steps(int count, bool jumpStart = false) => new StepsEasing(Math.Max(1, count), jumpStart);

    /// <summary>Maps the fraction of time to the fraction of the change.</summary>
    /// <param name="t">The fraction of the time that has passed, from 0 to 1.</param>
    /// <returns>The fraction of the change: 0 at the start and 1 at the end. A curve that overshoots goes outside 0..1 in between.</returns>
    public abstract double Evaluate(double t);

    /// <summary>Tells whether another curve is the same: it is written the same way.</summary>
    /// <param name="obj">The other object.</param>
    /// <returns><see langword="true"/> for a curve with the same name and numbers.</returns>
    public override bool Equals(object? obj) => obj is Easing other && other.GetType() == GetType() && other.ToString() == ToString();

    /// <summary>Gets a hash code that matches <see cref="Equals(object?)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => ToString()!.GetHashCode(StringComparison.Ordinal);

    /// <summary>Tries to read a curve: a keyword, <c>cubic-bezier(...)</c> or <c>steps(...)</c>.</summary>
    /// <param name="text">The text, such as <c>ease-out</c>, <c>cubic-bezier(0.2, 0, 0, 1)</c>, <c>steps(4)</c> or <c>steps(4, start)</c>.</param>
    /// <param name="easing">The curve, or <see cref="Ease"/> when the text could not be read.</param>
    /// <returns><see langword="false"/> for an unknown keyword or bad arguments.</returns>
    public static bool TryParse(string text, out Easing easing)
    {
        easing = Ease;

        text = text.Trim();

        switch (text)
        {
            case "linear":
                easing = Linear;
                return true;

            case "ease":
                return true;

            case "ease-in":
                easing = EaseIn;
                return true;

            case "ease-out":
                easing = EaseOut;
                return true;

            case "ease-in-out":
                easing = EaseInOut;
                return true;
        }

        if (!Function.TryRead(text, out var name, out var arguments))
            return false;

        if (name == "cubic-bezier" && arguments.Length == 4 && TryNumbers(arguments, out var n))
        {
            easing = CubicBezier(n[0], n[1], n[2], n[3]);

            return true;
        }

        if (name == "steps" && arguments.Length is 1 or 2 && int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 1)
        {
            var start = arguments.Length == 2 && arguments[1] is "start" or "jump-start";

            if (arguments.Length == 2 && !(arguments[1] is "start" or "end" or "jump-start" or "jump-end"))
                return false;

            easing = Steps(count, start);

            return true;
        }

        return false;
    }

    /// <summary>Reads numbers.</summary>
    static bool TryNumbers(string[] texts, out double[] numbers)
    {
        numbers = new double[texts.Length];

        for (var i = 0; i < texts.Length; i++)
            if (!double.TryParse(texts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || !double.IsFinite(numbers[i]))
                return false;

        return true;
    }

    /// <summary>The curve that changes evenly.</summary>
    sealed class LinearEasing : Easing
    {
        /// <inheritdoc/>
        public override double Evaluate(double t) => t;

        /// <inheritdoc/>
        public override string ToString() => "linear";
    }

    /// <summary>A cubic Bézier curve through (0, 0) and (1, 1).</summary>
    sealed class CubicBezierEasing(double x1, double y1, double x2, double y2, string? name = null) : Easing
    {
        /// <inheritdoc/>
        public override double Evaluate(double t)
        {
            if (t <= 0)
                return 0;

            if (t >= 1)
                return 1;

            // Find the parameter whose x is t, with Newton's method and bisection as a fallback, and return its y.
            var u = t;

            for (var i = 0; i < 8; i++)
            {
                var error = Sample(u, x1, x2) - t;

                if (Math.Abs(error) < 1e-7)
                    return Sample(u, y1, y2);

                var slope = Slope(u, x1, x2);

                if (Math.Abs(slope) < 1e-6)
                    break;

                u -= error / slope;
            }

            var (low, high) = (0.0, 1.0);

            u = t;

            for (var i = 0; i < 40; i++)
            {
                var x = Sample(u, x1, x2);

                if (Math.Abs(x - t) < 1e-7)
                    break;

                if (x < t)
                    low = u;
                else
                    high = u;

                u = (low + high) / 2;
            }

            return Sample(u, y1, y2);
        }

        /// <inheritdoc/>
        public override string ToString() => name ?? string.Create(CultureInfo.InvariantCulture, $"cubic-bezier({x1}, {y1}, {x2}, {y2})");

        /// <summary>Gets one coordinate of the curve at a parameter.</summary>
        static double Sample(double u, double a, double b) => (((((1 - (3 * b)) + (3 * a)) * u) + ((3 * b) - (6 * a))) * u + (3 * a)) * u;

        /// <summary>Gets the slope of one coordinate at a parameter.</summary>
        static double Slope(double u, double a, double b) => (3 * ((1 - (3 * b)) + (3 * a)) * u * u) + (2 * ((3 * b) - (6 * a)) * u) + (3 * a);
    }

    /// <summary>A curve that jumps in equal steps.</summary>
    sealed class StepsEasing(int count, bool jumpStart) : Easing
    {
        /// <inheritdoc/>
        public override double Evaluate(double t)
        {
            if (t <= 0)
                return jumpStart ? 1.0 / count : 0;

            if (t >= 1)
                return 1;

            var step = Math.Floor(t * count) + (jumpStart ? 1 : 0);

            return Math.Min(1, step / count);
        }

        /// <inheritdoc/>
        public override string ToString() => $"steps({count}, {(jumpStart ? "start" : "end")})";
    }
}

/// <summary>Reads the <c>name(arguments)</c> shape that values such as easings and transforms share.</summary>
static class Function
{
    /// <summary>Splits a function call into its name and its arguments, which are separated by commas or white space.</summary>
    /// <param name="text">The text, such as <c>steps(4, end)</c>.</param>
    /// <param name="name">The name.</param>
    /// <param name="arguments">The trimmed, non-empty arguments.</param>
    /// <returns><see langword="false"/> when the text is not one call.</returns>
    public static bool TryRead(string text, out string name, out string[] arguments)
    {
        name = string.Empty;
        arguments = [];

        var open = text.IndexOf('(');

        if (open <= 0 || !text.EndsWith(')') || text.IndexOf(')') != text.Length - 1)
            return false;

        name = text[..open].Trim();
        arguments = text[(open + 1)..^1].Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return true;
    }
}
