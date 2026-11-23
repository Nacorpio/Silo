using System.Globalization;

namespace Silo;

/// <summary>
/// One column of a <see cref="DataGrid"/>: its header, how a cell value is read from a row, and how the column is sized.
/// </summary>
/// <remarks>
/// <para>
/// The value of a cell comes from <see cref="Binding"/>, a function from the row to a value, so no reflection is needed. It
/// is shown with <see cref="Format"/> when it is <see cref="IFormattable"/>, and with <c>ToString()</c> otherwise.
/// </para>
/// <para>
/// A column is either fixed, with <see cref="Width"/> in pixels, or proportional: when <see cref="Star"/> is above 0 the
/// column takes its share of the width that the fixed columns leave, in proportion to the Star of the other proportional
/// columns, but never less than <see cref="MinWidth"/>. Dragging the edge of a header makes the column fixed.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var name = new DataGridColumn("Name", row => ((Person)row).Name) { Star = 1 };
/// var age = new DataGridColumn("Age", row => ((Person)row).Age) { Width = 60, Alignment = TextAlign.Right };
/// </code>
/// </example>
public sealed class DataGridColumn
{
    /// <summary>Creates a column.</summary>
    /// <param name="header">The text of the header.</param>
    /// <param name="binding">Reads the value of a cell from a row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> or <paramref name="binding"/> is <see langword="null"/>.</exception>
    public DataGridColumn(string header, Func<object, object?> binding)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(binding);

        Header = header;
        Binding = binding;
    }

    /// <summary>Gets or sets the text of the header.</summary>
    public string Header { get; set; }

    /// <summary>Gets or sets the function that reads the value of a cell from a row.</summary>
    public Func<object, object?> Binding { get; set; }

    /// <summary>Gets or sets the format string that is used for values that are <see cref="IFormattable"/>, such as <c>"N2"</c> or <c>"d"</c>. The default is <see langword="null"/>.</summary>
    public string? Format { get; set; }

    /// <summary>Gets or sets the width in pixels of a fixed column. The default is 100.</summary>
    public double Width { get; set; } = 100;

    /// <summary>Gets or sets the share of the remaining width. The default is 0, which makes the column fixed.</summary>
    public double Star { get; set; }

    /// <summary>Gets or sets the smallest width in pixels, also while dragging the edge. The default is 40.</summary>
    public double MinWidth { get; set; } = 40;

    /// <summary>Gets or sets where the text of the cells stands in the column. The default is <see cref="TextAlign.Left"/>.</summary>
    public TextAlign Alignment { get; set; }

    /// <summary>Gets or sets how the values are ordered when the grid is sorted by this column. The default is <see langword="null"/>, which orders by the natural order of the values.</summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>Gets the text of a cell.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The value formatted with <see cref="Format"/> and the current culture, or an empty text for <see langword="null"/>.</returns>
    public string TextOf(object row) => Binding(row) switch
    {
        null => string.Empty,
        IFormattable formattable when Format is not null => formattable.ToString(Format, CultureInfo.CurrentCulture),
        var value => value.ToString() ?? string.Empty,
    };
}
