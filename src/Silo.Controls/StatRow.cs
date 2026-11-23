namespace Silo;

/// <summary>One clickable row of a <see cref="StatList"/>. It is a Button, so it has the hover and pressed states and a click.</summary>
public sealed class StatRow : Button
{
    /// <summary>Runs before the first StatRow is created, so that the Styled properties are registered in time.</summary>
    static StatRow()
    {
    }
}
