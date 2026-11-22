namespace Silo.Studio;

/// <summary>
/// The text of a code editor with a caret, a selection, editing commands and undo. It has no Controls and no drawing, so the
/// rules of editing can be tested, and the editor Control only shows what the buffer holds.
/// </summary>
/// <remarks>
/// <para>
/// Positions are offsets into <see cref="Text"/>, which only has <c>\n</c> as a line break. A buffer made from text with
/// <c>\r\n</c> breaks remembers it, and <see cref="ToFileText"/> writes the breaks back, so opening and saving a file does
/// not change its line endings.
/// </para>
/// <para>
/// Typing is merged in undo: a run of letters, or a run of backspaces, is one step. A new line, a paste, a deletion of a
/// selection or a move of the caret starts a new step.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var buffer = new TextBuffer("{\n  \"rules\": {}\n}");
///
/// buffer.MoveToEnd(extend: false);
/// buffer.Insert("\n");
/// buffer.Undo();
/// </code>
/// </example>
public sealed class TextBuffer
{
    /// <summary>The text that is inserted for one press of Tab.</summary>
    public const string Indent = "  ";

    /// <summary>The line break that the text had when it was loaded, written back by <see cref="ToFileText"/>.</summary>
    string lineBreak;

    /// <summary>The states that <see cref="Undo"/> goes back to, newest last.</summary>
    readonly Stack<State> undo = new();

    /// <summary>The states that <see cref="Redo"/> goes forward to, newest last.</summary>
    readonly Stack<State> redo = new();

    /// <summary>The current state.</summary>
    State state;

    /// <summary>The column that Up and Down try to keep, or -1 until a vertical move sets it.</summary>
    int desiredColumn = -1;

    /// <summary>The kind of the last edit, which decides whether the next one is merged with it.</summary>
    Kind last = Kind.None;

    /// <summary>Creates a buffer.</summary>
    /// <param name="text">The text, with <c>\n</c> or <c>\r\n</c> line breaks.</param>
    public TextBuffer(string text = "")
    {
        ArgumentNullException.ThrowIfNull(text);

        lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        state = new(text.Replace("\r\n", "\n", StringComparison.Ordinal), 0, 0);
    }

    /// <summary>Raised after the text, the caret or the selection changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the text, with <c>\n</c> as the only line break.</summary>
    public string Text => state.Text;

    /// <summary>Gets the offset of the caret, which is the moving end of the selection.</summary>
    public int Caret => state.Caret;

    /// <summary>Gets the offset where the selection started, which is <see cref="Caret"/> when nothing is selected.</summary>
    public int Anchor => state.Anchor;

    /// <summary>Gets a value indicating whether any text is selected.</summary>
    public bool HasSelection => state.Caret != state.Anchor;

    /// <summary>Gets the offset where the selection starts.</summary>
    public int SelectionStart => Math.Min(state.Caret, state.Anchor);

    /// <summary>Gets the offset where the selection ends.</summary>
    public int SelectionEnd => Math.Max(state.Caret, state.Anchor);

    /// <summary>Gets the selected text.</summary>
    public string SelectedText => state.Text[SelectionStart..SelectionEnd];

    /// <summary>Gets the number of lines, which is one more than the number of line breaks.</summary>
    public int LineCount => state.Text.Count(c => c == '\n') + 1;

    /// <summary>Gets the zero-based line of the caret.</summary>
    public int Line => LineOf(state.Caret);

    /// <summary>Gets the zero-based column of the caret.</summary>
    public int Column => state.Caret - LineStart(Line);

    /// <summary>Gets a value indicating whether <see cref="Undo"/> has a step to take back.</summary>
    public bool CanUndo => undo.Count > 0;

    /// <summary>Gets a value indicating whether <see cref="Redo"/> has a step to apply again.</summary>
    public bool CanRedo => redo.Count > 0;

    /// <summary>Gets the text as it is written to a file, with the line breaks that it was loaded with.</summary>
    /// <returns>The text.</returns>
    public string ToFileText()
        => lineBreak == "\n" ? state.Text : state.Text.Replace("\n", lineBreak, StringComparison.Ordinal);

    /// <summary>Gets the zero-based line that holds an offset.</summary>
    /// <param name="offset">The offset, from 0 to the length of the text.</param>
    /// <returns>The line.</returns>
    public int LineOf(int offset)
    {
        var line = 0;

        for (var i = 0; i < Math.Min(offset, state.Text.Length); i++)
            if (state.Text[i] == '\n')
                line++;

        return line;
    }

    /// <summary>Gets the offset where a line starts.</summary>
    /// <param name="line">The zero-based line, from 0 to <see cref="LineCount"/> minus one.</param>
    /// <returns>The offset.</returns>
    public int LineStart(int line)
    {
        var offset = 0;

        for (var i = 0; i < line; i++)
            offset = state.Text.IndexOf('\n', offset) + 1;

        return offset;
    }

    /// <summary>Gets the offset where a line ends, before its line break.</summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns>The offset.</returns>
    public int LineEnd(int line)
    {
        var end = state.Text.IndexOf('\n', LineStart(line));

        return end < 0 ? state.Text.Length : end;
    }

    /// <summary>Gets the text of a line without its line break.</summary>
    /// <param name="line">The zero-based line.</param>
    /// <returns>The text.</returns>
    public string GetLine(int line) => state.Text[LineStart(line)..LineEnd(line)];

    /// <summary>Replaces all of the text, which also clears the selection and the undo steps and takes over its line breaks.</summary>
    /// <param name="text">The new text.</param>
    public void Reset(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        state = new(text.Replace("\r\n", "\n", StringComparison.Ordinal), 0, 0);

        undo.Clear();
        redo.Clear();

        last = Kind.None;

        Raise();
    }

    /// <summary>Puts the caret at an offset.</summary>
    /// <param name="offset">The offset, which is limited to the text.</param>
    /// <param name="extend">Whether to keep the anchor and select up to the offset.</param>
    public void MoveTo(int offset, bool extend)
    {
        var caret = Math.Clamp(offset, 0, state.Text.Length);

        desiredColumn = -1;
        last = Kind.None;

        state = state with { Caret = caret, Anchor = extend ? state.Anchor : caret };

        Raise();
    }

    /// <summary>Moves the caret one character to the left. A selection collapses to its start.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    public void MoveLeft(bool extend)
        => MoveTo(HasSelection && !extend ? SelectionStart : state.Caret - 1, extend);

    /// <summary>Moves the caret one character to the right. A selection collapses to its end.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    public void MoveRight(bool extend)
        => MoveTo(HasSelection && !extend ? SelectionEnd : state.Caret + 1, extend);

    /// <summary>Moves the caret one line up, keeping its column where the line is long enough.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    public void MoveUp(bool extend) => MoveLines(-1, extend);

    /// <summary>Moves the caret one line down, keeping its column where the line is long enough.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    public void MoveDown(bool extend) => MoveLines(1, extend);

    /// <summary>Moves the caret to the first character of its line, or to the first character that is not a space.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    /// <remarks>Pressed again at the first non-space character, it goes to the start of the line.</remarks>
    public void MoveToLineStart(bool extend)
    {
        var start = LineStart(Line);
        var text = GetLine(Line);
        var first = start + (text.Length - text.TrimStart(' ', '\t').Length);

        MoveTo(state.Caret == first ? start : first, extend);
    }

    /// <summary>Moves the caret to the end of its line.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    public void MoveToLineEnd(bool extend) => MoveTo(LineEnd(Line), extend);

    /// <summary>Moves the caret to the end of the text.</summary>
    /// <param name="extend">Whether to select while moving.</param>
    public void MoveToEnd(bool extend) => MoveTo(state.Text.Length, extend);

    /// <summary>Selects all of the text.</summary>
    public void SelectAll()
    {
        last = Kind.None;

        state = state with { Anchor = 0, Caret = state.Text.Length };

        Raise();
    }

    /// <summary>Types text, which replaces the selection.</summary>
    /// <param name="text">The text. Its <c>\r\n</c> breaks become <c>\n</c>.</param>
    /// <remarks>One character that is not a line break is merged with the typing before it in undo.</remarks>
    public void Insert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);

        if (normalized.Length == 0 && !HasSelection)
            return;

        var typing = normalized.Length == 1 && normalized[0] != '\n' && !HasSelection;

        Edit(SelectionStart, SelectionEnd, normalized, typing ? Kind.Typing : Kind.Other);
    }

    /// <summary>Starts a new line, indented like the line it was started in.</summary>
    /// <remarks>After an opening brace or bracket at the end of the line, the new line is indented one step further.</remarks>
    public void NewLine()
    {
        var before = state.Text[LineStart(Line)..SelectionStart];
        var indent = before[..(before.Length - before.TrimStart(' ').Length)];
        var opens = before.TrimEnd().EndsWith('{') || before.TrimEnd().EndsWith('[');

        Edit(SelectionStart, SelectionEnd, "\n" + indent + (opens ? Indent : string.Empty), Kind.Other);
    }

    /// <summary>Deletes the selection, or the character before the caret.</summary>
    public void Backspace()
    {
        if (HasSelection)
            Edit(SelectionStart, SelectionEnd, string.Empty, Kind.Other);
        else if (state.Caret > 0)
            Edit(state.Caret - 1, state.Caret, string.Empty, Kind.Backspacing);
    }

    /// <summary>Deletes the selection, or the character after the caret.</summary>
    public void Delete()
    {
        if (HasSelection)
            Edit(SelectionStart, SelectionEnd, string.Empty, Kind.Other);
        else if (state.Caret < state.Text.Length)
            Edit(state.Caret, state.Caret + 1, string.Empty, Kind.Deleting);
    }

    /// <summary>Indents: inserts a step at the caret, or indents every line that the selection touches.</summary>
    public void Tab()
    {
        if (!HasSelection || LineOf(SelectionStart) == LineOf(SelectionEnd))
        {
            Edit(SelectionStart, SelectionEnd, Indent, Kind.Other);

            return;
        }

        ChangeLines(line => Indent + line);
    }

    /// <summary>Takes one step of indentation off the line of the caret, or off every line that the selection touches.</summary>
    public void Untab() => ChangeLines(Outdent);

    /// <summary>Takes back the last step.</summary>
    /// <returns><see langword="false"/> when there is nothing to take back.</returns>
    public bool Undo()
    {
        if (undo.Count == 0)
            return false;

        redo.Push(state);

        state = undo.Pop();
        last = Kind.None;

        Raise();

        return true;
    }

    /// <summary>Applies the step that <see cref="Undo"/> took back.</summary>
    /// <returns><see langword="false"/> when there is nothing to apply.</returns>
    public bool Redo()
    {
        if (redo.Count == 0)
            return false;

        undo.Push(state);

        state = redo.Pop();
        last = Kind.None;

        Raise();

        return true;
    }

    /// <summary>Takes one step of indentation off a line, or all of its spaces when there is less than a step.</summary>
    /// <param name="line">The text of the line.</param>
    /// <returns>The new text.</returns>
    static string Outdent(string line)
        => line.StartsWith(Indent, StringComparison.Ordinal) ? line[Indent.Length..] : line.TrimStart(' ');

    /// <summary>Moves the caret by lines.</summary>
    /// <param name="delta">The number of lines, negative for up.</param>
    /// <param name="extend">Whether to select while moving.</param>
    void MoveLines(int delta, bool extend)
    {
        var line = Line + delta;

        if (line < 0)
        {
            MoveTo(0, extend);

            return;
        }

        if (line >= LineCount)
        {
            MoveTo(state.Text.Length, extend);

            return;
        }

        var column = desiredColumn < 0 ? Column : desiredColumn;
        var target = Math.Min(LineStart(line) + column, LineEnd(line));

        MoveTo(target, extend);

        desiredColumn = column;
    }

    /// <summary>Changes every line that the selection touches.</summary>
    /// <param name="change">Gives the new text of a line.</param>
    void ChangeLines(Func<string, string> change)
    {
        var first = LineOf(SelectionStart);
        var lastLine = LineOf(SelectionEnd);

        if (HasSelection && SelectionEnd == LineStart(lastLine) && lastLine > first)
            lastLine--;

        var start = LineStart(first);
        var end = LineEnd(lastLine);
        var lines = state.Text[start..end].Split('\n').Select(change);
        var replaced = string.Join('\n', lines);

        Edit(start, end, replaced, Kind.Other);

        state = state with { Anchor = start, Caret = start + replaced.Length };

        Raise();
    }

    /// <summary>Replaces a range of the text and puts the caret after the new text.</summary>
    /// <param name="start">The first offset of the range.</param>
    /// <param name="end">The offset after the range.</param>
    /// <param name="replacement">The new text.</param>
    /// <param name="kind">The kind of the edit, which decides if it is merged with the one before.</param>
    void Edit(int start, int end, string replacement, Kind kind)
    {
        if (kind == Kind.Other || kind != last)
            undo.Push(state);

        redo.Clear();

        var caret = kind == Kind.Deleting ? start : start + replacement.Length;
        var text = string.Concat(state.Text.AsSpan(0, start), replacement, state.Text.AsSpan(end));

        state = new(text, caret, caret);
        desiredColumn = -1;
        last = kind;

        Raise();
    }

    /// <summary>Raises <see cref="Changed"/>.</summary>
    void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>The text with the caret and the anchor, which is what undo goes back to.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="Caret">The caret.</param>
    /// <param name="Anchor">The anchor.</param>
    sealed record State(string Text, int Caret, int Anchor);

    /// <summary>The kinds of edit, which decide how an edit is merged in undo.</summary>
    enum Kind
    {
        /// <summary>No edit yet, or the caret moved since.</summary>
        None,

        /// <summary>A character was typed.</summary>
        Typing,

        /// <summary>Backspace deleted a character.</summary>
        Backspacing,

        /// <summary>Delete deleted a character.</summary>
        Deleting,

        /// <summary>Any edit that is a step of its own.</summary>
        Other,
    }
}
