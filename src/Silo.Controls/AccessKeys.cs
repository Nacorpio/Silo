namespace Silo;

/// <summary>Reads the access key out of a text such as <c>&amp;File</c>, where the letter after the ampersand is the key.</summary>
static class AccessKeys
{
    /// <summary>Splits a text into what is shown and its access key.</summary>
    /// <param name="raw">The text. <c>&amp;</c> marks the next letter as the access key and <c>&amp;&amp;</c> is a literal ampersand.</param>
    /// <returns>The text without the markers, the index of the access key in it or -1, and the key as an upper-case letter.</returns>
    public static (string Text, int Index, char? Key) Parse(string raw)
    {
        var text = new System.Text.StringBuilder();
        var index = -1;
        char? key = null;

        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '&' || i == raw.Length - 1)
            {
                text.Append(raw[i]);

                continue;
            }

            i++;

            if (raw[i] != '&' && index < 0 && char.IsLetterOrDigit(raw[i]))
            {
                index = text.Length;
                key = char.ToUpperInvariant(raw[i]);
            }

            text.Append(raw[i]);
        }

        return (text.ToString(), index, key);
    }
}
