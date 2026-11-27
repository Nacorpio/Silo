using Silo.Rendering;

namespace Silo;

/// <summary>
/// Splits the lines of a <see cref="TextBuffer"/> into the rows that a wrapping <see cref="TextArea"/> draws, and keeps the result
/// until a line or the width changes.
/// </summary>
/// <remarks>
/// <para>
/// A line breaks after its last space that fits. A word that is wider than the width breaks where it no longer fits. A space that
/// does not fit stays at the end of its row, so no row starts with a space that was a break. Without a width every line is one row.
/// </para>
/// <para>
/// The rows of a line are cached with the text they were made from. A line whose text did not change, at the same width, font and
/// measurer, is not measured again.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var map = new WrapMap();
///
/// map.Update(buffer, measurer, style, width: 200);
///
/// Console.WriteLine(map.TotalRows);
/// </code>
/// </example>
internal sealed class WrapMap
{
    /// <summary>The rows of each line, with the text that they were made from.</summary>
    readonly List<(string Text, int[] Starts)> lines = [];

    /// <summary>The number of rows before each line, with the total at the end.</summary>
    int[] firstRows = [0];

    /// <summary>The measurer that the rows were made with.</summary>
    ITextMeasurer? measurer;

    /// <summary>The font that the rows were made with.</summary>
    TextStyle style;

    /// <summary>The width that the rows were made for.</summary>
    double width = double.NaN;

    /// <summary>Gets the number of rows of all lines.</summary>
    public int TotalRows => firstRows[^1];

    /// <summary>Brings the rows up to date.</summary>
    /// <param name="buffer">The text.</param>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="style">The font.</param>
    /// <param name="width">The width of a row in pixels, or 0 or less for no wrapping.</param>
    public void Update(TextBuffer buffer, ITextMeasurer measurer, TextStyle style, double width)
    {
        width = width > 0 ? width : 0;

        if (!ReferenceEquals(this.measurer, measurer) || this.style != style || this.width != width)
        {
            lines.Clear();

            this.measurer = measurer;
            this.style = style;
            this.width = width;
        }

        var count = buffer.LineCount;

        if (lines.Count > count)
            lines.RemoveRange(count, lines.Count - count);

        var first = new int[count + 1];

        for (var i = 0; i < count; i++)
        {
            var text = buffer.GetLine(i);

            if (i >= lines.Count)
                lines.Add((text, Break(measurer, text, style, width)));
            else if (lines[i].Text != text)
                lines[i] = (text, Break(measurer, text, style, width));

            first[i + 1] = first[i] + lines[i].Starts.Length;
        }

        firstRows = first;
    }

    /// <summary>Gets the number of rows of a line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The number of rows, at least 1.</returns>
    public int RowsIn(int line) => lines[line].Starts.Length;

    /// <summary>Gets the number of rows before a line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The number of rows.</returns>
    public int FirstRow(int line) => firstRows[Math.Clamp(line, 0, firstRows.Length - 1)];

    /// <summary>Finds the line and the row in it that a row of the whole text is.</summary>
    /// <param name="row">The row, counted from the top of the text. It is clamped to the text.</param>
    /// <returns>The line, and the row within the line.</returns>
    public (int Line, int Row) Locate(int row)
    {
        row = Math.Clamp(row, 0, Math.Max(0, TotalRows - 1));

        var low = 0;
        var high = lines.Count - 1;

        while (low < high)
        {
            var middle = (low + high + 1) / 2;

            if (firstRows[middle] <= row)
                low = middle;
            else
                high = middle - 1;
        }

        return (low, row - firstRows[low]);
    }

    /// <summary>Gets where a row starts in its line.</summary>
    /// <param name="line">The line.</param>
    /// <param name="row">The row within the line.</param>
    /// <returns>The column of the first character.</returns>
    public int StartOf(int line, int row) => lines[line].Starts[row];

    /// <summary>Gets where a row ends in its line.</summary>
    /// <param name="line">The line.</param>
    /// <param name="row">The row within the line.</param>
    /// <returns>The column after the last character.</returns>
    public int EndOf(int line, int row)
        => row + 1 < lines[line].Starts.Length ? lines[line].Starts[row + 1] : lines[line].Text.Length;

    /// <summary>Finds the row of a line that a column is on.</summary>
    /// <param name="line">The line.</param>
    /// <param name="column">The column. The start of a row belongs to that row, not to the one before it.</param>
    /// <returns>The row within the line.</returns>
    public int RowOf(int line, int column)
    {
        var starts = lines[line].Starts;
        var row = 0;

        while (row + 1 < starts.Length && starts[row + 1] <= column)
            row++;

        return row;
    }

    /// <summary>Splits a line into rows.</summary>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="text">The line.</param>
    /// <param name="style">The font.</param>
    /// <param name="width">The width of a row, or 0 for no wrapping.</param>
    /// <returns>The column where each row starts. The first is 0.</returns>
    public static int[] Break(ITextMeasurer measurer, string text, TextStyle style, double width)
    {
        if (width <= 0 || text.Length < 2)
            return [0];

        var layout = new TextLayout(measurer, text, style);

        if (layout.Width <= width)
            return [0];

        var starts = new List<int> { 0 };
        var start = 0;

        while (true)
        {
            var from = layout.WidthTo(start);

            if (layout.WidthTo(text.Length) - from <= width)
                break;

            var fit = start + 1;

            while (fit < text.Length && layout.WidthTo(fit + 1) - from <= width)
                fit++;

            if (fit >= text.Length)
                break;

            var next = fit;

            if (text[fit] == ' ')
                next = fit + 1;
            else
            {
                var space = text.LastIndexOf(' ', fit - 1, fit - start);

                if (space >= start)
                    next = space + 1;
            }

            if (next <= start)
                next = start + 1;

            if (next >= text.Length)
                break;

            starts.Add(next);

            start = next;
        }

        return [.. starts];
    }
}
