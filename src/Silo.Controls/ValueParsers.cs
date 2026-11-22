using System.Globalization;
using Silo.Layout;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Parsers for the keyword and number values of the Styled properties that Controls declare.
/// </summary>
/// <remarks>Each method has the shape of <see cref="ValueParser{T}"/>, so it can be passed as a method group.</remarks>
internal static class ValueParsers
{
    /// <summary>Parses a length or the keyword <c>auto</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The length, or <see langword="null"/> for <c>auto</c>.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    public static bool AutoLength(string text, out Length? value)
    {
        text = text.Trim();

        if (text == "auto")
        {
            value = null;

            return true;
        }

        var parsed = Length.TryParse(text, out var length);

        value = parsed ? length : null;

        return parsed;
    }

    /// <summary>Parses a length that is always present, such as a gap.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The length.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    public static bool RequiredLength(string text, out Length value) => Length.TryParse(text, out value);

    /// <summary>Parses a size in pixels such as <c>4px</c>, <c>4</c> or <c>0.5rem</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The size in pixels.</param>
    /// <returns><see langword="true"/> when the text is a length without a percentage.</returns>
    public static bool Pixels(string text, out double value)
    {
        value = 0;

        if (!Length.TryParse(text, out var length))
            return false;

        if (Lengths.Resolve(length, 0) is not { } pixels)
            return false;

        value = pixels;

        return length.Unit != LengthUnit.Percent;
    }

    /// <summary>Parses a plain number.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when the text is a finite number.</returns>
    public static bool Number(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>Parses <c>row</c> or <c>column</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The direction.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool Direction(string text, out FlexDirection value)
    {
        FlexDirection? parsed = text.Trim() switch
        {
            "row" => FlexDirection.Row,
            "column" => FlexDirection.Column,

            _ => null,
        };

        value = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>Parses the <c>justify-content</c> keywords.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The distribution mode.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool Justify(string text, out JustifyContent value)
    {
        JustifyContent? parsed = text.Trim() switch
        {
            "start" or "flex-start" => JustifyContent.Start,
            "center" => JustifyContent.Center,
            "end" or "flex-end" => JustifyContent.End,
            "space-between" => JustifyContent.SpaceBetween,
            "space-around" => JustifyContent.SpaceAround,
            "space-evenly" => JustifyContent.SpaceEvenly,

            _ => null,
        };

        value = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>Parses the <c>align-items</c> keywords.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The alignment.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool Align(string text, out AlignItems value)
    {
        AlignItems? parsed = text.Trim() switch
        {
            "stretch" => AlignItems.Stretch,
            "start" or "flex-start" => AlignItems.Start,
            "center" => AlignItems.Center,
            "end" or "flex-end" => AlignItems.End,

            _ => null,
        };

        value = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>Parses the <c>align-self</c> keywords, where <c>auto</c> means the parent's <c>align-items</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The alignment, or <see langword="null"/> for <c>auto</c>.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool AlignSelf(string text, out AlignItems? value)
    {
        if (text.Trim() == "auto")
        {
            value = null;

            return true;
        }

        var parsed = Align(text, out var align);

        value = parsed ? align : null;

        return parsed;
    }

    /// <summary>Parses <c>normal</c>, <c>bold</c> or a numeric weight where 600 and above is bold.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value"><see langword="true"/> for bold.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    public static bool Weight(string text, out bool value)
    {
        text = text.Trim();
        value = text == "bold";

        if (text is "bold" or "normal")
            return true;

        if (!Number(text, out var weight))
            return false;

        value = weight >= 600;

        return true;
    }

    /// <summary>Parses <c>left</c>, <c>center</c> or <c>right</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The alignment.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool TextAlignment(string text, out TextAlign value)
    {
        TextAlign? parsed = text.Trim() switch
        {
            "left" or "start" => TextAlign.Left,
            "center" => TextAlign.Center,
            "right" or "end" => TextAlign.Right,

            _ => null,
        };

        value = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>Parses <c>left</c>, <c>top</c> or <c>right</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The position.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool IconPlacement(string text, out IconPosition value)
    {
        IconPosition? parsed = text.Trim() switch
        {
            "left" => IconPosition.Left,
            "top" => IconPosition.Top,
            "right" => IconPosition.Right,

            _ => null,
        };

        value = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>Parses <c>left</c> or <c>right</c>.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The position.</param>
    /// <returns><see langword="true"/> when the text is a known keyword.</returns>
    public static bool ButtonPlacement(string text, out ButtonPosition value)
    {
        ButtonPosition? parsed = text.Trim() switch
        {
            "left" => ButtonPosition.Left,
            "right" => ButtonPosition.Right,

            _ => null,
        };

        value = parsed.GetValueOrDefault();

        return parsed is not null;
    }

    /// <summary>Parses a string value, which is always valid and keeps the text as written.</summary>
    /// <param name="text">The Computed text.</param>
    /// <param name="value">The trimmed text, without surrounding quotes.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public static bool Text(string text, out string value)
    {
        value = text.Trim().Trim('"', '\'');

        return true;
    }
}
