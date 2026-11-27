namespace Silo;

/// <summary>
/// One emoji of the <see cref="EmojiCatalog"/>.
/// </summary>
/// <param name="Value">The emoji itself, as a string of one or more code points.</param>
/// <param name="Name">The Unicode name, in lower case, such as <c>"grinning face"</c>.</param>
/// <param name="Group">The group it is listed under, such as <c>"Smileys &amp; Emotion"</c>.</param>
/// <param name="Keywords">The search keywords from CLDR. Some emoji, such as flags, have none; their name is what is searched.</param>
/// <param name="Tones">The five skin tone variants, from light to dark, or an empty list when the emoji has no tones.</param>
public sealed record EmojiEntry(string Value, string Name, string Group, IReadOnlyList<string> Keywords, IReadOnlyList<string> Tones)
{
    /// <summary>Gets whether the emoji has skin tone variants.</summary>
    public bool HasTones => Tones.Count > 0;

    /// <summary>Gets the emoji in a skin tone.</summary>
    /// <param name="tone">The skin tone.</param>
    /// <returns>The variant, or <see cref="Value"/> when the tone is <see cref="SkinTone.Default"/> or the emoji has no tones.</returns>
    public string In(SkinTone tone) => tone == SkinTone.Default || !HasTones ? Value : Tones[(int)tone - 1];
}
