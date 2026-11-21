namespace Silo.Styling.Values;

/// <summary>Margin, padding or border widths in CSS shorthand order: top, right, bottom, left.</summary>
public readonly record struct Thickness(Length Top, Length Right, Length Bottom, Length Left)
{
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
