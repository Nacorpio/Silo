namespace Silo;

/// <summary>Helpers for <see cref="Key"/>.</summary>
public static class KeyExtensions
{
    /// <summary>Gets the letter of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The upper-case letter for <see cref="Key.A"/> to <see cref="Key.Z"/>, otherwise <see langword="null"/>.</returns>
    /// <example>
    /// <code language="csharp">
    /// var letter = Key.F.Letter(); // 'F'
    /// </code>
    /// </example>
    public static char? Letter(this Key key)
        => key is >= Key.A and <= Key.Z ? (char)('A' + (key - Key.A)) : null;
}
