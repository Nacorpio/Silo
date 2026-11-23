namespace Silo.Styling.Values;

/// <summary>Works out the values between two values of a Styled property, which is what lets a property change smoothly.</summary>
/// <remarks>
/// Numbers, <see cref="Color"/>, <see cref="Length"/>, <see cref="Thickness"/>, <see cref="Transform"/> and <see cref="TransformOrigin"/> can be
/// interpolated. Lengths only when both have the same unit, since a pixel width and a percentage cannot be blended without the layout, and not when
/// one of them is missing (<c>auto</c>). Other values, such as words and flags, change at once.
/// </remarks>
public static class Interpolator
{
    /// <summary>Tries to work out the value at a fraction of the way from one value to another.</summary>
    /// <param name="from">The value at 0.</param>
    /// <param name="to">The value at 1. It has to be of the same type.</param>
    /// <param name="t">The fraction. It can go below 0 or above 1 where a curve overshoots, except for colors, which are kept in range.</param>
    /// <param name="value">The value between them.</param>
    /// <returns><see langword="false"/> when the two values cannot be blended.</returns>
    public static bool TryLerp(object? from, object? to, double t, out object? value)
    {
        value = null;

        switch (from, to)
        {
            case (double a, double b):
                value = a + ((b - a) * t);
                return true;

            case (Color a, Color b):
                value = new Color(Channel(a.R, b.R, t), Channel(a.G, b.G, t), Channel(a.B, b.B, t), Channel(a.A, b.A, t));
                return true;

            case (Length a, Length b) when TryLerp(a, b, t, out var length):
                value = length;
                return true;

            case (Thickness a, Thickness b) when TryLerp(a.Top, b.Top, t, out var top) && TryLerp(a.Right, b.Right, t, out var right)
                && TryLerp(a.Bottom, b.Bottom, t, out var bottom) && TryLerp(a.Left, b.Left, t, out var left):
                value = new Thickness(top, right, bottom, left);
                return true;

            case (Transform a, Transform b):
                value = new Transform(
                    a.TranslateX + ((b.TranslateX - a.TranslateX) * t),
                    a.TranslateY + ((b.TranslateY - a.TranslateY) * t),
                    a.Rotate + ((b.Rotate - a.Rotate) * t),
                    a.ScaleX + ((b.ScaleX - a.ScaleX) * t),
                    a.ScaleY + ((b.ScaleY - a.ScaleY) * t));
                return true;

            case (TransformOrigin a, TransformOrigin b) when TryLerp(a.X, b.X, t, out var x) && TryLerp(a.Y, b.Y, t, out var y):
                value = new TransformOrigin(x, y);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Tells whether two values can be blended.</summary>
    /// <param name="from">The first value.</param>
    /// <param name="to">The second value.</param>
    /// <returns><see langword="true"/> when <see cref="TryLerp(object?, object?, double, out object?)"/> works for them.</returns>
    public static bool CanInterpolate(object? from, object? to) => TryLerp(from, to, 0.5, out _);

    /// <summary>Blends two lengths with the same unit.</summary>
    static bool TryLerp(Length a, Length b, double t, out Length length)
    {
        length = default;

        if (a.Unit != b.Unit)
            return false;

        length = new Length(a.Value + ((b.Value - a.Value) * t), a.Unit);

        return true;
    }

    /// <summary>Blends one color channel and keeps it in range.</summary>
    static byte Channel(byte a, byte b, double t) => (byte)Math.Clamp(Math.Round(a + ((b - a) * t)), 0, 255);
}
