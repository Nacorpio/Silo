using System.Text;

namespace Silo;

/// <summary>
/// Edits the line that the user types, the way a terminal driver does for a program that cannot: it keeps the text and the cursor,
/// reacts to the editing keys, remembers the lines of a history, and produces the escape sequences that show the changes.
/// </summary>
/// <remarks>
/// <para>
/// Feed it what <see cref="Terminal"/> sends for the keys. Printable text is inserted at the cursor; Backspace, Delete, Left, Right,
/// Home, End, Up and Down (history), Ctrl+A, Ctrl+E, Ctrl+K, Ctrl+U and Ctrl+W edit the line; Enter completes it; Ctrl+C throws the
/// line away and Ctrl+D ends the input when the line is empty. Other escape sequences are ignored.
/// </para>
/// <para>
/// The output only moves the cursor with relative sequences and never repaints the prompt, so it works after any prompt or partial
/// output that the program printed. A line that is longer than the terminal is wide wraps visually but is not edited correctly.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var editor = new TerminalLineEditor();
/// var edit = editor.Feed("ls -la\r");
///
/// terminal.Write(edit.Output);
///
/// foreach (var line in edit.Lines)
///     Run(line);
/// </code>
/// </example>
public sealed class TerminalLineEditor
{
    readonly List<string> history = [];

    readonly StringBuilder text = new();

    int cursor;

    int historyIndex;

    string draft = string.Empty;

    /// <summary>Gets or sets the most lines the history keeps. The default is 100.</summary>
    public int HistoryLimit { get; set; } = 100;

    /// <summary>Gets the lines that were entered, oldest first.</summary>
    public IReadOnlyList<string> History => history;

    /// <summary>Gets the text of the line being edited.</summary>
    public string Text => text.ToString();

    /// <summary>Gets the position of the cursor in <see cref="Text"/>.</summary>
    public int Cursor => cursor;

    /// <summary>What happened when text was fed to a <see cref="TerminalLineEditor"/>.</summary>
    /// <param name="Output">The escape sequences and text to show on the terminal.</param>
    /// <param name="Lines">The lines that were completed with Enter, in order.</param>
    /// <param name="Interrupted">Whether Ctrl+C was pressed. The line is thrown away.</param>
    /// <param name="EndOfInput">Whether Ctrl+D was pressed on an empty line.</param>
    /// <param name="ClearRequested">Whether Ctrl+L was pressed, which asks the owner to clear the screen and show the prompt and line again.</param>
    public readonly record struct Edit(string Output, IReadOnlyList<string> Lines, bool Interrupted, bool EndOfInput, bool ClearRequested);

    /// <summary>Applies typed text to the line.</summary>
    /// <param name="input">The text, with the escape sequences of special keys. Pasted text can hold several lines.</param>
    /// <returns>The change to show and what it completed.</returns>
    public Edit Feed(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var output = new StringBuilder();
        var lines = new List<string>();
        var (interrupted, ended, clear) = (false, false, false);

        for (var i = 0; i < input.Length; i++)
        {
            var character = input[i];

            switch (character)
            {
                case '\u001b':
                    i = Escape(input, i, output);
                    break;

                case '\r':
                case '\n':
                    if (character == '\n' && i > 0 && input[i - 1] == '\r')
                        break;

                    Enter(output, lines);
                    break;

                case '\u007f' or '\b':
                    if (cursor > 0)
                        Replace(output, text.ToString().Remove(cursor - 1, 1), cursor - 1);

                    break;

                case '\u0001':
                    Replace(output, text.ToString(), 0);
                    break;

                case '\u0005':
                    Replace(output, text.ToString(), text.Length);
                    break;

                case '\u000b':
                    Replace(output, text.ToString()[..cursor], cursor);
                    break;

                case '\u0015':
                    Replace(output, text.ToString()[cursor..], 0);
                    break;

                case '\u0017':
                    DeleteWordBack(output);
                    break;

                case '\u000c':
                    clear = true;
                    break;

                case '\u0003':
                    output.Append("^C\r\n");
                    text.Clear();

                    cursor = 0;
                    historyIndex = history.Count;
                    interrupted = true;

                    break;

                case '\u0004':
                    if (text.Length == 0)
                        ended = true;
                    else if (cursor < text.Length)
                        Replace(output, text.ToString().Remove(cursor, 1), cursor);

                    break;

                default:
                    if (character >= ' ')
                        Insert(output, character);

                    break;
            }
        }

        return new Edit(output.ToString(), lines, interrupted, ended, clear);
    }

    /// <summary>Gets the escape sequences that put the cursor back after the screen was cleared and the prompt was written again.</summary>
    /// <returns>The text of the line followed by the moves that put the cursor in its place.</returns>
    public string Redraw() => text.ToString() + Left(text.Length - cursor);

    /// <summary>Throws the current line away without showing anything, for example after the screen was cleared.</summary>
    public void Reset()
    {
        text.Clear();

        cursor = 0;
        historyIndex = history.Count;
    }

    /// <summary>Handles an escape sequence that starts at an index.</summary>
    /// <param name="input">The input.</param>
    /// <param name="start">The index of the escape character.</param>
    /// <param name="output">Where the changes to show are added.</param>
    /// <returns>The index of the last character of the sequence.</returns>
    int Escape(string input, int start, StringBuilder output)
    {
        if (start + 1 >= input.Length)
            return start;

        var kind = input[start + 1];

        if (kind == 'O' && start + 2 < input.Length)
        {
            Key(input[start + 2], 0, output);

            return start + 2;
        }

        if (kind != '[')
            return start + 1;

        var end = start + 2;
        var number = 0;

        while (end < input.Length && (char.IsDigit(input[end]) || input[end] == ';'))
        {
            if (char.IsDigit(input[end]))
                number = (number * 10) + (input[end] - '0');

            end++;
        }

        if (end < input.Length)
            Key(input[end], number, output);

        return Math.Min(end, input.Length - 1);
    }

    /// <summary>Handles a cursor or editing key given as the final character of a sequence.</summary>
    /// <param name="final">The final character.</param>
    /// <param name="number">The first number of the sequence.</param>
    /// <param name="output">Where the changes to show are added.</param>
    void Key(char final, int number, StringBuilder output)
    {
        switch (final)
        {
            case 'D' when cursor > 0:
                Replace(output, text.ToString(), cursor - 1);
                break;

            case 'C' when cursor < text.Length:
                Replace(output, text.ToString(), cursor + 1);
                break;

            case 'H':
                Replace(output, text.ToString(), 0);
                break;

            case 'F':
                Replace(output, text.ToString(), text.Length);
                break;

            case 'A':
                Browse(output, -1);
                break;

            case 'B':
                Browse(output, 1);
                break;

            case '~' when number == 3 && cursor < text.Length:
                Replace(output, text.ToString().Remove(cursor, 1), cursor);
                break;

            case '~' when number == 1:
                Replace(output, text.ToString(), 0);
                break;

            case '~' when number == 4:
                Replace(output, text.ToString(), text.Length);
                break;
        }
    }

    /// <summary>Inserts a character at the cursor.</summary>
    /// <param name="output">Where the changes to show are added.</param>
    /// <param name="character">The character.</param>
    void Insert(StringBuilder output, char character)
    {
        if (cursor == text.Length)
        {
            text.Append(character);

            cursor++;
            output.Append(character);

            return;
        }

        Replace(output, text.ToString().Insert(cursor, character.ToString()), cursor + 1);
    }

    /// <summary>Completes the line.</summary>
    /// <param name="output">Where the changes to show are added.</param>
    /// <param name="lines">Where the completed line is added.</param>
    void Enter(StringBuilder output, List<string> lines)
    {
        var line = text.ToString();

        output.Append(Right(text.Length - cursor)).Append("\r\n");
        lines.Add(line);

        if (line.Length > 0 && (history.Count == 0 || history[^1] != line))
            history.Add(line);

        if (history.Count > HistoryLimit)
            history.RemoveRange(0, history.Count - HistoryLimit);

        text.Clear();

        cursor = 0;
        historyIndex = history.Count;
    }

    /// <summary>Deletes the word before the cursor and the spaces between it and the cursor.</summary>
    /// <param name="output">Where the changes to show are added.</param>
    void DeleteWordBack(StringBuilder output)
    {
        var start = cursor;

        while (start > 0 && text[start - 1] == ' ')
            start--;

        while (start > 0 && text[start - 1] != ' ')
            start--;

        Replace(output, text.ToString().Remove(start, cursor - start), start);
    }

    /// <summary>Replaces the line with an older or newer one from the history.</summary>
    /// <param name="output">Where the changes to show are added.</param>
    /// <param name="direction">-1 for older, 1 for newer.</param>
    void Browse(StringBuilder output, int direction)
    {
        var target = Math.Clamp(historyIndex + direction, 0, history.Count);

        if (target == historyIndex)
            return;

        if (historyIndex == history.Count)
            draft = text.ToString();

        historyIndex = target;

        var line = target == history.Count ? draft : history[target];

        Replace(output, line, line.Length);
    }

    /// <summary>Changes the line and the cursor, and adds the escape sequences that show the change using only relative moves.</summary>
    /// <param name="output">Where the changes to show are added.</param>
    /// <param name="newText">The new text of the line.</param>
    /// <param name="newCursor">The new position of the cursor.</param>
    void Replace(StringBuilder output, string newText, int newCursor)
    {
        var oldLength = text.Length;

        output.Append(Left(cursor)).Append(newText);

        var shown = newText.Length;

        if (oldLength > shown)
        {
            output.Append(' ', oldLength - shown).Append(Left(oldLength - shown));
        }

        output.Append(Left(shown - newCursor));

        text.Clear().Append(newText);

        cursor = newCursor;
    }

    /// <summary>Builds the sequence that moves the cursor left.</summary>
    /// <param name="count">The number of columns.</param>
    static string Left(int count) => count > 0 ? $"\u001b[{count}D" : string.Empty;

    /// <summary>Builds the sequence that moves the cursor right.</summary>
    /// <param name="count">The number of columns.</param>
    static string Right(int count) => count > 0 ? $"\u001b[{count}C" : string.Empty;
}
