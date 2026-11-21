using System.Globalization;

namespace Silo.Styling.Values;

/// <summary>
/// An sRGB color with 8 bits per channel and a straight (not premultiplied) alpha channel.
/// </summary>
/// <param name="R">The red channel, from 0 to 255.</param>
/// <param name="G">The green channel, from 0 to 255.</param>
/// <param name="B">The blue channel, from 0 to 255.</param>
/// <param name="A">The alpha channel, from 0 (fully transparent) to 255 (fully opaque, the default).</param>
/// <remarks>
/// Use <see cref="TryParse"/> to read the CSS-style color strings that Stylesheets use. Colors written in other color spaces
/// are converted to sRGB and clamped to the 0 to 255 range per channel, so a color outside the sRGB gamut is clipped, not
/// mapped to the nearest in-gamut color.
/// </remarks>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    static readonly Dictionary<string, Color> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = new(0, 0, 0, 0),
        ["black"] = new(0, 0, 0),
        ["white"] = new(255, 255, 255),
        ["red"] = new(255, 0, 0),
        ["green"] = new(0, 128, 0),
        ["blue"] = new(0, 0, 255),
        ["gray"] = new(128, 128, 128),
        ["grey"] = new(128, 128, 128),
        ["yellow"] = new(255, 255, 0),
        ["orange"] = new(255, 165, 0),
        ["purple"] = new(128, 0, 128),
    };

    /// <summary>
    /// Tries to read a CSS-style color string. Nothing is thrown for bad input.
    /// </summary>
    /// <param name="text">
    /// The color text. Leading and trailing white space is ignored. The supported forms are:
    /// <list type="bullet">
    /// <item><description>Hex: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c> and <c>#rrggbbaa</c>.</description></item>
    /// <item><description>A named color, matched without regard to case: <c>transparent</c>, <c>black</c>, <c>white</c>, <c>red</c>, <c>green</c>, <c>blue</c>, <c>gray</c>, <c>grey</c>, <c>yellow</c>, <c>orange</c> and <c>purple</c>. This is a small subset of the CSS names.</description></item>
    /// <item><description><c>rgb(r g b)</c> and <c>rgba(r, g, b, a)</c>: each channel is a number from 0 to 255 or a percentage.</description></item>
    /// <item><description><c>hsl(h s l)</c> and <c>hsla(h, s, l, a)</c>: the hue is in degrees, with an optional <c>deg</c> suffix, and the saturation and lightness are percentages or fractions from 0 to 1.</description></item>
    /// <item><description><c>oklch(l c h)</c>: lightness is a fraction or percentage, chroma is a number (about 0 to 0.4 for real colors) and the hue is in degrees.</description></item>
    /// </list>
    /// In the function forms the arguments may be separated by commas, spaces or a slash, and the alpha is an optional fourth
    /// argument given as a fraction from 0 to 1 or as a percentage. Function names are matched without regard to case.
    /// </param>
    /// <param name="color">
    /// When the method returns <see langword="true"/>, the parsed color; otherwise <see langword="default"/>, which is
    /// transparent black.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="text"/> is a supported color; <see langword="false"/> for anything else,
    /// including unknown functions, the wrong number of arguments and numbers that cannot be read.
    /// </returns>
    /// <remarks>
    /// Out-of-range values are clamped instead of rejected, for example <c>rgb(300 0 0)</c> is the same as
    /// <c>rgb(255 0 0)</c>. Numbers are read with the invariant culture.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// Color.TryParse("#3b82f6", out var blue);                  // true: Color(59, 130, 246, 255)
    /// Color.TryParse("rgb(10 20 30 / 50%)", out var faded);     // true: Color(10, 20, 30, 128)
    /// Color.TryParse("hsl(120, 100%, 25%)", out var green);     // true: Color(0, 128, 0, 255)
    /// Color.TryParse("oklch(0.628 0.2577 29.23)", out var red); // true: close to Color(255, 0, 0, 255)
    ///
    /// Color.TryParse("not a color", out _);                     // false
    /// </code>
    /// </example>
    public static bool TryParse(string text, out Color color)
    {
        color = default;

        text = text.Trim();

        if (text.StartsWith('#'))
        {
            return Hex(text[1..], out color);
        }

        if (Named.TryGetValue(text, out color))
        {
            return true;
        }

        var open = text.IndexOf('(');

        if (open < 0 || !text.EndsWith(')'))
        {
            return false;
        }

        var function = text[..open].Trim().ToLowerInvariant();

        var arguments = text[(open + 1)..^1]
            .Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (arguments.Length is not (3 or 4))
        {
            return false;
        }

        var alpha = arguments.Length == 4 ? Fraction(arguments[3]) : 1;

        if (alpha is null)
        {
            return false;
        }

        switch (function)
        {
            case "rgb" or "rgba":
            {
                if (Channel(arguments[0]) is { } r && Channel(arguments[1]) is { } g && Channel(arguments[2]) is { } b)
                {
                    color = new Color(r, g, b, ToByte(alpha.Value));

                    return true;
                }

                return false;
            }

            case "hsl" or "hsla":
            {
                if (Number(arguments[0].TrimEnd('d', 'e', 'g')) is { } h && Fraction(arguments[1]) is { } s && Fraction(arguments[2]) is { } l)
                {
                    color = FromHsl(h, s, l, alpha.Value);

                    return true;
                }

                return false;
            }

            case "oklch":
            {
                if (Fraction(arguments[0]) is { } lightness && Number(arguments[1]) is { } chroma && Number(arguments[2].TrimEnd('d', 'e', 'g')) is { } hue)
                {
                    color = FromOklch(lightness, chroma, hue, alpha.Value);

                    return true;
                }

                return false;
            }

            default:
            {
                return false;
            }
        }
    }

    static bool Hex(string digits, out Color color)
    {
        color = default;

        if (digits.Length is 3 or 4)
        {
            digits = string.Concat(digits.Select(c => $"{c}{c}"));
        }

        if (digits.Length is not (6 or 8) || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = digits.Length == 6
            ? new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : new Color((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);

        return true;
    }

    static double? Number(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;

    /// <summary>Reads "0.5" or "50%" as a fraction of one.</summary>
    static double? Fraction(string text)
        => text.EndsWith('%') ? Number(text[..^1]) / 100 : Number(text);

    static byte? Channel(string text)
        => text.EndsWith('%') ? Number(text[..^1]) is { } p ? ToByte(p / 100) : null : Number(text) is { } n ? ToByte(n / 255) : null;

    static byte ToByte(double fraction)
        => (byte)Math.Round(Math.Clamp(fraction, 0, 1) * 255);

    static Color FromHsl(double hue, double saturation, double lightness, double alpha)
    {
        hue = (hue % 360 + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);

        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - c / 2;

        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return new Color(ToByte(r + m), ToByte(g + m), ToByte(b + m), ToByte(alpha));
    }

    static Color FromOklch(double lightness, double chroma, double hue, double alpha)
    {
        var radians = hue * Math.PI / 180;
        var a = chroma * Math.Cos(radians);
        var b = chroma * Math.Sin(radians);

        var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);

        var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var blue = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        return new Color(ToByte(Gamma(r)), ToByte(Gamma(g)), ToByte(Gamma(blue)), ToByte(alpha));
    }

    static double Gamma(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);

        return linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
    }
}
