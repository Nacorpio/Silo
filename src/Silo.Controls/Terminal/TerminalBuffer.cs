using System.Text;

namespace Silo;

/// <summary>
/// The screen of a terminal: a grid of cells with a cursor, a scroll region and a scrollback of the lines that have scrolled off
/// the top. It has no idea of escape sequences; <see cref="TerminalParser"/> turns those into calls on this class.
/// </summary>
/// <remarks>
/// <para>
/// Rows and columns count from 0, with row 0 at the top. Text written with <see cref="Write"/> takes the current pen
/// (<see cref="Foreground"/>, <see cref="Background"/>, <see cref="Attributes"/>). Erasing fills with the pen's background, as real
/// terminals do. When the cursor writes in the last column the wrap waits until the next character, so a full line followed by a
/// line feed does not leave an empty line.
/// </para>
/// <para>
/// A character is always one cell wide: wide East Asian characters and combining marks are not handled yet.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var buffer = new TerminalBuffer(80, 24);
///
/// buffer.Write(new Rune('A'));
/// buffer.LineFeed();
///
/// var cell = buffer.GetCell(0, 0);
/// </code>
/// </example>
public sealed class TerminalBuffer
{
    /// <summary>The default number of lines kept in the scrollback.</summary>
    public const int DefaultScrollbackLimit = 1000;

    /// <summary>The distance between tab stops.</summary>
    const int TabWidth = 8;

    readonly List<TerminalCell[]> scrollback = [];

    TerminalCell[][] screen;

    TerminalCell[][]? mainScreen;

    int scrollTop;

    int scrollBottom;

    bool pendingWrap;

    (int Column, int Row, TerminalColor Foreground, TerminalColor Background, TerminalAttributes Attributes, bool PendingWrap)? saved;

    int scrollbackLimit = DefaultScrollbackLimit;

    string title = string.Empty;

    /// <summary>Raised when a program changes the window title.</summary>
    public event EventHandler? TitleChanged;

    /// <summary>Creates a blank screen.</summary>
    /// <param name="columns">The number of columns, at least 1.</param>
    /// <param name="rows">The number of rows, at least 1.</param>
    public TerminalBuffer(int columns, int rows)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        screen = NewScreen(Columns, Rows);
        scrollBottom = Rows - 1;
    }

    /// <summary>Gets the number of columns.</summary>
    public int Columns { get; private set; }

    /// <summary>Gets the number of rows.</summary>
    public int Rows { get; private set; }

    /// <summary>Gets the column of the cursor.</summary>
    public int CursorColumn { get; private set; }

    /// <summary>Gets the row of the cursor.</summary>
    public int CursorRow { get; private set; }

    /// <summary>Gets or sets whether the cursor is shown. The default is <see langword="true"/>.</summary>
    public bool CursorVisible { get; set; } = true;

    /// <summary>Gets or sets whether text wraps to the next line at the last column. The default is <see langword="true"/>.</summary>
    public bool AutoWrap { get; set; } = true;

    /// <summary>Gets or sets whether the arrow keys send application sequences instead of cursor sequences. The default is <see langword="false"/>.</summary>
    public bool ApplicationCursorKeys { get; set; }

    /// <summary>Gets or sets whether pasted text is wrapped in bracket markers so that the program can tell it from typing. The default is <see langword="false"/>.</summary>
    public bool BracketedPaste { get; set; }

    /// <summary>Gets whether the alternate screen, which has no scrollback and is used by full-screen programs, is shown.</summary>
    public bool IsAlternateScreen => mainScreen is not null;

    /// <summary>Gets or sets the text color of the pen.</summary>
    public TerminalColor Foreground { get; set; }

    /// <summary>Gets or sets the background color of the pen.</summary>
    public TerminalColor Background { get; set; }

    /// <summary>Gets or sets the text styles of the pen.</summary>
    public TerminalAttributes Attributes { get; set; }

    /// <summary>Gets the first row of the scroll region.</summary>
    public int ScrollTop => scrollTop;

    /// <summary>Gets the last row of the scroll region.</summary>
    public int ScrollBottom => scrollBottom;

    /// <summary>Gets the number of lines in the scrollback.</summary>
    public int ScrollbackCount => scrollback.Count;

    /// <summary>Gets or sets how many lines the scrollback keeps. The oldest lines are dropped first. The default is 1000.</summary>
    public int ScrollbackLimit
    {
        get => scrollbackLimit;
        set
        {
            scrollbackLimit = Math.Max(0, value);

            TrimScrollback();
        }
    }

    /// <summary>Gets the window title that a program set, or an empty text.</summary>
    public string Title
    {
        get => title;
        set
        {
            if (title == value)
                return;

            title = value;

            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets a cell of the screen.</summary>
    /// <param name="column">The column.</param>
    /// <param name="row">The row.</param>
    /// <returns>The cell.</returns>
    public TerminalCell GetCell(int column, int row) => screen[row][column];

    /// <summary>Gets a line of the scrollback.</summary>
    /// <param name="index">The index, where 0 is the oldest line.</param>
    /// <returns>The cells of the line. The line can be shorter or longer than <see cref="Columns"/> if the terminal was resized.</returns>
    public IReadOnlyList<TerminalCell> GetScrollbackRow(int index) => scrollback[index];

    /// <summary>Gets the text of a row of the screen, without trailing spaces.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The text.</returns>
    public string GetRowText(int row) => TextOf(screen[row]);

    /// <summary>Gets the text of a line of the scrollback, without trailing spaces.</summary>
    /// <param name="index">The index, where 0 is the oldest line.</param>
    /// <returns>The text.</returns>
    public string GetScrollbackText(int index) => TextOf(scrollback[index]);

    /// <summary>Writes a character at the cursor with the current pen and moves the cursor on.</summary>
    /// <param name="character">The character.</param>
    public void Write(Rune character)
    {
        if (pendingWrap && AutoWrap)
        {
            CursorColumn = 0;

            LineFeed();
        }

        pendingWrap = false;

        screen[CursorRow][CursorColumn] = new TerminalCell(character, Foreground, Background, Attributes);

        if (CursorColumn < Columns - 1)
            CursorColumn++;
        else
            pendingWrap = true;
    }

    /// <summary>Moves the cursor down a row, scrolling the scroll region when it is on the last row of it.</summary>
    public void LineFeed()
    {
        pendingWrap = false;

        if (CursorRow == scrollBottom)
            ScrollUp(1);
        else if (CursorRow < Rows - 1)
            CursorRow++;
    }

    /// <summary>Moves the cursor up a row, scrolling the scroll region down when it is on the first row of it.</summary>
    public void ReverseIndex()
    {
        pendingWrap = false;

        if (CursorRow == scrollTop)
            ScrollDown(1);
        else if (CursorRow > 0)
            CursorRow--;
    }

    /// <summary>Moves the cursor to the first column.</summary>
    public void CarriageReturn()
    {
        CursorColumn = 0;
        pendingWrap = false;
    }

    /// <summary>Moves the cursor one column to the left, if it can.</summary>
    public void Backspace()
    {
        CursorColumn = Math.Max(0, CursorColumn - 1);
        pendingWrap = false;
    }

    /// <summary>Moves the cursor to the next tab stop, or to the last column.</summary>
    public void Tab()
    {
        CursorColumn = Math.Min(Columns - 1, ((CursorColumn / TabWidth) + 1) * TabWidth);
        pendingWrap = false;
    }

    /// <summary>Moves the cursor to a position, clamped to the screen.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    public void MoveTo(int row, int column)
    {
        CursorRow = Math.Clamp(row, 0, Rows - 1);
        CursorColumn = Math.Clamp(column, 0, Columns - 1);
        pendingWrap = false;
    }

    /// <summary>Moves the cursor up, stopping at the top of the scroll region when it is inside it.</summary>
    /// <param name="count">The number of rows.</param>
    public void CursorUp(int count) => MoveTo(Math.Max(CursorRow - count, CursorRow >= scrollTop ? scrollTop : 0), CursorColumn);

    /// <summary>Moves the cursor down, stopping at the bottom of the scroll region when it is inside it.</summary>
    /// <param name="count">The number of rows.</param>
    public void CursorDown(int count) => MoveTo(Math.Min(CursorRow + count, CursorRow <= scrollBottom ? scrollBottom : Rows - 1), CursorColumn);

    /// <summary>Moves the cursor to the right.</summary>
    /// <param name="count">The number of columns.</param>
    public void CursorForward(int count) => MoveTo(CursorRow, CursorColumn + count);

    /// <summary>Moves the cursor to the left.</summary>
    /// <param name="count">The number of columns.</param>
    public void CursorBack(int count) => MoveTo(CursorRow, CursorColumn - count);

    /// <summary>Sets the scroll region, the rows that scroll when the cursor moves past the bottom, and moves the cursor to the top left.</summary>
    /// <param name="top">The first row.</param>
    /// <param name="bottom">The last row. A region of fewer than two rows resets it to the whole screen.</param>
    public void SetScrollRegion(int top, int bottom)
    {
        top = Math.Clamp(top, 0, Rows - 1);
        bottom = Math.Clamp(bottom, 0, Rows - 1);

        if (bottom <= top)
            (top, bottom) = (0, Rows - 1);

        scrollTop = top;
        scrollBottom = bottom;

        MoveTo(0, 0);
    }

    /// <summary>Scrolls the scroll region up, so that the first lines go to the scrollback when the region starts at the top of the main screen.</summary>
    /// <param name="count">The number of lines.</param>
    public void ScrollUp(int count) => Shift(scrollTop, scrollBottom, count, up: true, keep: scrollTop == 0 && !IsAlternateScreen);

    /// <summary>Scrolls the scroll region down, leaving blank lines at its top.</summary>
    /// <param name="count">The number of lines.</param>
    public void ScrollDown(int count) => Shift(scrollTop, scrollBottom, count, up: false, keep: false);

    /// <summary>Erases part of the screen.</summary>
    /// <param name="mode">0 erases from the cursor to the end, 1 from the start to the cursor, 2 everything, 3 everything and the scrollback.</param>
    public void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                EraseCells(CursorRow, CursorColumn, Columns);

                for (var row = CursorRow + 1; row < Rows; row++)
                    screen[row] = BlankRow();

                break;

            case 1:
                for (var row = 0; row < CursorRow; row++)
                    screen[row] = BlankRow();

                EraseCells(CursorRow, 0, CursorColumn + 1);

                break;

            case 2 or 3:
                for (var row = 0; row < Rows; row++)
                    screen[row] = BlankRow();

                if (mode == 3)
                    scrollback.Clear();

                break;
        }
    }

    /// <summary>Erases part of the cursor's line.</summary>
    /// <param name="mode">0 erases from the cursor to the end, 1 from the start to the cursor, 2 the whole line.</param>
    public void EraseInLine(int mode)
    {
        switch (mode)
        {
            case 0:
                EraseCells(CursorRow, CursorColumn, Columns);
                break;

            case 1:
                EraseCells(CursorRow, 0, CursorColumn + 1);
                break;

            case 2:
                EraseCells(CursorRow, 0, Columns);
                break;
        }
    }

    /// <summary>Erases characters from the cursor to the right without moving the rest of the line.</summary>
    /// <param name="count">The number of characters.</param>
    public void EraseCharacters(int count) => EraseCells(CursorRow, CursorColumn, Math.Min(Columns, CursorColumn + Math.Max(1, count)));

    /// <summary>Inserts blank lines at the cursor row, pushing the lines below it down to the bottom of the scroll region.</summary>
    /// <param name="count">The number of lines.</param>
    public void InsertLines(int count)
    {
        if (CursorRow < scrollTop || CursorRow > scrollBottom)
            return;

        Shift(CursorRow, scrollBottom, count, up: false, keep: false);
        CarriageReturn();
    }

    /// <summary>Deletes lines at the cursor row, pulling the lines below it up and leaving blank lines at the bottom of the scroll region.</summary>
    /// <param name="count">The number of lines.</param>
    public void DeleteLines(int count)
    {
        if (CursorRow < scrollTop || CursorRow > scrollBottom)
            return;

        Shift(CursorRow, scrollBottom, count, up: true, keep: false);
        CarriageReturn();
    }

    /// <summary>Inserts blank cells at the cursor, pushing the rest of the line to the right. Cells pushed off the end are lost.</summary>
    /// <param name="count">The number of cells.</param>
    public void InsertCharacters(int count)
    {
        count = Math.Clamp(count, 1, Columns - CursorColumn);

        var line = screen[CursorRow];

        Array.Copy(line, CursorColumn, line, CursorColumn + count, Columns - CursorColumn - count);

        EraseCells(CursorRow, CursorColumn, CursorColumn + count);
    }

    /// <summary>Deletes cells at the cursor, pulling the rest of the line to the left and leaving blanks at its end.</summary>
    /// <param name="count">The number of cells.</param>
    public void DeleteCharacters(int count)
    {
        count = Math.Clamp(count, 1, Columns - CursorColumn);

        var line = screen[CursorRow];

        Array.Copy(line, CursorColumn + count, line, CursorColumn, Columns - CursorColumn - count);

        EraseCells(CursorRow, Columns - count, Columns);
    }

    /// <summary>Remembers the cursor position and the pen, to be restored by <see cref="RestoreCursor"/>.</summary>
    public void SaveCursor() => saved = (CursorColumn, CursorRow, Foreground, Background, Attributes, pendingWrap);

    /// <summary>Moves the cursor back to the remembered position and pen, or to the top left with the default pen when nothing was saved.</summary>
    public void RestoreCursor()
    {
        if (saved is not { } state)
        {
            MoveTo(0, 0);
            ResetPen();

            return;
        }

        MoveTo(state.Row, state.Column);

        (Foreground, Background, Attributes, pendingWrap) = (state.Foreground, state.Background, state.Attributes, state.PendingWrap);
    }

    /// <summary>Resets the pen to the default colors and no styles.</summary>
    public void ResetPen() => (Foreground, Background, Attributes) = (default, default, TerminalAttributes.None);

    /// <summary>Switches to or from the alternate screen. Leaving it brings back the main screen as it was.</summary>
    /// <param name="alternate"><see langword="true"/> to show the alternate screen, which starts blank.</param>
    public void UseAlternateScreen(bool alternate)
    {
        if (alternate == IsAlternateScreen)
            return;

        if (alternate)
        {
            mainScreen = screen;
            screen = NewScreen(Columns, Rows);

            return;
        }

        screen = mainScreen!;
        mainScreen = null;
    }

    /// <summary>Changes the size of the screen. Lines that no longer fit at the top go to the scrollback and the scroll region is reset.</summary>
    /// <param name="columns">The new number of columns, at least 1.</param>
    /// <param name="rows">The new number of rows, at least 1.</param>
    public void Resize(int columns, int rows)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);

        if (columns == Columns && rows == Rows)
            return;

        var lines = screen.ToList();
        var shift = Math.Max(0, Math.Min(lines.Count - rows, CursorRow - (rows - 1)));

        for (var i = 0; i < shift; i++)
        {
            if (!IsAlternateScreen)
                AddToScrollback(lines[0]);

            lines.RemoveAt(0);
        }

        if (lines.Count > rows)
            lines.RemoveRange(rows, lines.Count - rows);

        while (lines.Count < rows)
            lines.Add(BlankRow(columns));

        for (var i = 0; i < lines.Count; i++)
            lines[i] = Fit(lines[i], columns);

        screen = [.. lines];

        if (mainScreen is not null)
            mainScreen = Fit(mainScreen, columns, rows);

        CursorRow = Math.Clamp(CursorRow - shift, 0, rows - 1);
        CursorColumn = Math.Clamp(CursorColumn, 0, columns - 1);
        pendingWrap = false;

        Columns = columns;
        Rows = rows;
        scrollTop = 0;
        scrollBottom = rows - 1;
    }

    /// <summary>Puts the terminal back into the state it has when it starts: blank screen, no scrollback, default pen and modes.</summary>
    public void Reset()
    {
        mainScreen = null;
        screen = NewScreen(Columns, Rows);
        scrollback.Clear();
        scrollTop = 0;
        scrollBottom = Rows - 1;
        saved = null;
        CursorColumn = 0;
        CursorRow = 0;
        pendingWrap = false;
        CursorVisible = true;
        AutoWrap = true;
        ApplicationCursorKeys = false;
        BracketedPaste = false;

        ResetPen();
    }

    /// <summary>Gets the text of a row, without trailing spaces.</summary>
    /// <param name="cells">The cells.</param>
    static string TextOf(IReadOnlyList<TerminalCell> cells)
    {
        var text = new StringBuilder(cells.Count);

        foreach (var cell in cells)
            text.Append(cell.Character.ToString());

        return text.ToString().TrimEnd(' ');
    }

    /// <summary>Creates a blank screen.</summary>
    /// <param name="columns">The columns.</param>
    /// <param name="rows">The rows.</param>
    static TerminalCell[][] NewScreen(int columns, int rows)
    {
        var lines = new TerminalCell[rows][];

        for (var row = 0; row < rows; row++)
            lines[row] = Blank(columns, default);

        return lines;
    }

    /// <summary>Creates a row of blank cells with a background.</summary>
    /// <param name="columns">The number of cells.</param>
    /// <param name="background">The background color.</param>
    static TerminalCell[] Blank(int columns, TerminalColor background)
    {
        var cells = new TerminalCell[columns];

        Array.Fill(cells, new TerminalCell(new Rune(' '), default, background));

        return cells;
    }

    /// <summary>Makes a row exactly some columns wide, padding with blanks or cutting the end.</summary>
    /// <param name="line">The row.</param>
    /// <param name="columns">The number of columns.</param>
    static TerminalCell[] Fit(TerminalCell[] line, int columns)
    {
        if (line.Length == columns)
            return line;

        var fitted = Blank(columns, default);

        Array.Copy(line, fitted, Math.Min(line.Length, columns));

        return fitted;
    }

    /// <summary>Makes a screen exactly some columns and rows large.</summary>
    /// <param name="lines">The screen.</param>
    /// <param name="columns">The columns.</param>
    /// <param name="rows">The rows.</param>
    static TerminalCell[][] Fit(TerminalCell[][] lines, int columns, int rows)
    {
        var fitted = NewScreen(columns, rows);

        for (var row = 0; row < Math.Min(rows, lines.Length); row++)
            fitted[row] = Fit(lines[row], columns);

        return fitted;
    }

    /// <summary>Moves the rows between two rows up or down, filling the freed rows with blanks.</summary>
    /// <param name="top">The first row of the range.</param>
    /// <param name="bottom">The last row of the range.</param>
    /// <param name="count">The number of rows to move by.</param>
    /// <param name="up"><see langword="true"/> to move the rows up, which frees the bottom ones.</param>
    /// <param name="keep">Whether rows that leave at the top are added to the scrollback.</param>
    void Shift(int top, int bottom, int count, bool up, bool keep)
    {
        count = Math.Clamp(count, 0, bottom - top + 1);

        for (var i = 0; i < count; i++)
        {
            if (up)
            {
                if (keep)
                    AddToScrollback(screen[top]);

                Array.Copy(screen, top + 1, screen, top, bottom - top);

                screen[bottom] = BlankRow();

                continue;
            }

            Array.Copy(screen, top, screen, top + 1, bottom - top);

            screen[top] = BlankRow();
        }
    }

    /// <summary>Creates a blank row for the current width, filled with the pen's background.</summary>
    TerminalCell[] BlankRow() => Blank(Columns, Background);

    /// <summary>Creates a blank row for a width, filled with the pen's background.</summary>
    /// <param name="columns">The width.</param>
    TerminalCell[] BlankRow(int columns) => Blank(columns, Background);

    /// <summary>Erases cells of a row, filling them with the pen's background.</summary>
    /// <param name="row">The row.</param>
    /// <param name="from">The first column.</param>
    /// <param name="to">The column after the last one.</param>
    void EraseCells(int row, int from, int to)
    {
        var blank = new TerminalCell(new Rune(' '), default, Background);

        for (var column = Math.Max(0, from); column < Math.Min(Columns, to); column++)
            screen[row][column] = blank;
    }

    /// <summary>Adds a line to the scrollback and drops the oldest ones above the limit.</summary>
    /// <param name="line">The line.</param>
    void AddToScrollback(TerminalCell[] line)
    {
        if (scrollbackLimit == 0)
            return;

        scrollback.Add(line);

        TrimScrollback();
    }

    /// <summary>Drops the oldest lines above the limit.</summary>
    void TrimScrollback()
    {
        if (scrollback.Count > scrollbackLimit)
            scrollback.RemoveRange(0, scrollback.Count - scrollbackLimit);
    }
}
