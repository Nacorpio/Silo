namespace Silo;

/// <summary>Describes the new size of a <see cref="Terminal"/> in character cells, which the program has to be told about.</summary>
/// <param name="columns">The number of columns.</param>
/// <param name="rows">The number of rows.</param>
public sealed class TerminalResizedEventArgs(int columns, int rows) : EventArgs
{
    /// <summary>Gets the number of columns.</summary>
    public int Columns { get; } = columns;

    /// <summary>Gets the number of rows.</summary>
    public int Rows { get; } = rows;
}
