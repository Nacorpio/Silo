using System.Text;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A terminal screen that shows the output of a command line program and sends the keys that are typed back to it.
/// </summary>
/// <remarks>
/// <para>
/// A Terminal owns a <see cref="TerminalBuffer"/> and a <see cref="TerminalParser"/>. Give it the program's output with
/// <see cref="Write(string)"/> or <see cref="Write(ReadOnlySpan{byte})"/>, and listen to <see cref="Input"/> for what the user types
/// or pastes, which has to be sent to the program. When the control changes size the number of columns and rows follows, and
/// <see cref="Resized"/> tells the program. Connecting it to a process or to C# commands comes with the sessions.
/// </para>
/// <para>
/// The text is drawn on a fixed grid whose cells are as big as the character <c>M</c> in the <c>font-family</c> and <c>font-size</c>, so use
/// a monospace font. The Default theme uses a console font stack: Cascadia Mono, Consolas, Menlo and DejaVu Sans Mono. No font is
/// bundled. <c>terminal-line-height</c> is a factor of the font height, 1.2 by default.
/// </para>
/// <para>
/// The look comes from <c>terminal-foreground</c>, <c>terminal-background</c>, <c>terminal-color-0</c> to <c>terminal-color-15</c>
/// (the ANSI palette), <c>terminal-selection-color</c>, <c>terminal-cursor-color</c>, <c>terminal-cursor-style</c> (<c>block</c>,
/// <c>bar</c> or <c>underline</c>) and <c>terminal-cursor-blink</c>, the time of a blink in milliseconds, 0 for a steady cursor. Bold text
/// in the colors 0 to 7 is drawn in the colors 8 to 15. Blinking text is drawn steadily.
/// </para>
/// <para>
/// The mouse selects text, and a double press selects a word. Ctrl+Shift+C and Ctrl+Insert copy, Ctrl+Shift+V and Shift+Insert paste
/// (both need <see cref="Clipboard"/>), and plain Ctrl+C is sent to the program as usual. The mouse wheel and Shift+PageUp and
/// Shift+PageDown move through the scrollback; typing brings the screen back to the bottom. The scrollback is not shown on the alternate screen.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var terminal = new Terminal(80, 24);
///
/// terminal.Input += (_, e) => process.StandardInput.Write(e.Text);
/// terminal.Write("\u001b[32mready\u001b[0m\r\n$ ");
/// </code>
/// </example>
public class Terminal : Control, IScrollable, IPointerHandler
{
    /// <summary>The time in milliseconds in which a second press counts as a double press.</summary>
    const long DoublePressMilliseconds = 400;

    /// <summary>The levels of the 6x6x6 color cube of the 256-color palette.</summary>
    static readonly byte[] CubeLevels = [0, 95, 135, 175, 215, 255];

    /// <summary>The ANSI palette that is used when a Stylesheet gives none: the colors of a dark terminal.</summary>
    static readonly Color[] DefaultPalette =
    [
        new(0, 0, 0), new(205, 49, 49), new(13, 188, 121), new(229, 229, 16),
        new(36, 114, 200), new(188, 63, 188), new(17, 168, 205), new(229, 229, 229),
        new(102, 102, 102), new(241, 76, 76), new(35, 209, 139), new(245, 245, 67),
        new(59, 142, 234), new(214, 112, 214), new(41, 184, 219), new(255, 255, 255),
    ];

    /// <summary>The color of text that has no color.</summary>
    public static readonly StyledProperty<Color> TerminalForegroundProperty =
        StyledProperty.Register<Terminal, Color>("terminal-foreground", new(212, 212, 212), Color.TryParse);

    /// <summary>The color of the screen and of cells that have no background.</summary>
    public static readonly StyledProperty<Color> TerminalBackgroundProperty =
        StyledProperty.Register<Terminal, Color>("terminal-background", new(12, 15, 26), Color.TryParse);

    /// <summary>The color of the cursor.</summary>
    public static readonly StyledProperty<Color> CursorColorProperty =
        StyledProperty.Register<Terminal, Color>("terminal-cursor-color", new(212, 212, 212), Color.TryParse);

    /// <summary>The shape of the cursor: <c>block</c>, <c>bar</c> or <c>underline</c>.</summary>
    public static readonly StyledProperty<string> CursorStyleProperty =
        StyledProperty.Register<Terminal, string>("terminal-cursor-style", "block", ValueParsers.Text);

    /// <summary>The time of one blink of the cursor in milliseconds, or 0 for a cursor that does not blink.</summary>
    public static readonly StyledProperty<double> CursorBlinkProperty =
        StyledProperty.Register<Terminal, double>("terminal-cursor-blink", 530, ValueParsers.Pixels);

    /// <summary>The color that covers the selected cells.</summary>
    public static readonly StyledProperty<Color> SelectionColorProperty =
        StyledProperty.Register<Terminal, Color>("terminal-selection-color", new(59, 130, 246, 102), Color.TryParse);

    /// <summary>The height of a line as a factor of the font height.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        StyledProperty.Register<Terminal, double>("terminal-line-height", 1.2, ValueParsers.Pixels);

    /// <summary>The 16 colors of the ANSI palette, <c>terminal-color-0</c> to <c>terminal-color-15</c>.</summary>
    static readonly StyledProperty<Color>[] PaletteProperties = Enumerable.Range(0, 16)
        .Select(i => StyledProperty.Register<Terminal, Color>($"terminal-color-{i}", DefaultPalette[i], Color.TryParse))
        .ToArray();

    readonly TerminalBuffer buffer;

    readonly TerminalParser parser;

    ITerminalSession? session;

    Action<Action>? dispatcher;

    EventHandler<TerminalOutputEventArgs>? outputHandler;

    EventHandler<TerminalExitedEventArgs>? exitedHandler;

    (int Line, int Column)? anchor;

    (int Line, int Column) head;

    bool dragging;

    int scrollOffset;

    TimeSpan lastInput;

    long? lastPress;

    (int Line, int Column) lastPressCell;

    /// <summary>
    /// Runs before the first Terminal is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Terminal()
    {
    }

    /// <summary>Creates a Terminal with a blank screen.</summary>
    /// <param name="columns">The starting number of columns. It follows the size of the control after the first layout.</param>
    /// <param name="rows">The starting number of rows.</param>
    public Terminal(int columns = 80, int rows = 24)
    {
        buffer = new TerminalBuffer(columns, rows);
        parser = new TerminalParser(buffer) { Reply = Raise };
        parser.Bell += (_, _) => Bell?.Invoke(this, EventArgs.Empty);
        buffer.TitleChanged += (_, _) => TitleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the text has to be sent to the program: typed keys, pasted text, and answers to its questions.</summary>
    public event EventHandler<TerminalInputEventArgs>? Input;

    /// <summary>Raised when the number of columns or rows changed, so that the program can be told, for example with a window size signal.</summary>
    public event EventHandler<TerminalResizedEventArgs>? Resized;

    /// <summary>Raised when the program sets the window title.</summary>
    public event EventHandler? TitleChanged;

    /// <summary>Raised when the program rings the bell.</summary>
    public event EventHandler? Bell;

    /// <summary>Raised, on the dispatcher's thread, when the attached session has ended.</summary>
    public event EventHandler<TerminalExitedEventArgs>? Exited;

    /// <summary>Gets the attached session, or <see langword="null"/> when there is none.</summary>
    public ITerminalSession? Session => session;

    /// <summary>Gets the screen and scrollback.</summary>
    public TerminalBuffer Buffer => buffer;

    /// <summary>Gets the number of columns.</summary>
    public int Columns => buffer.Columns;

    /// <summary>Gets the number of rows.</summary>
    public int Rows => buffer.Rows;

    /// <summary>Gets the title that the program set, or an empty text.</summary>
    public string Title => buffer.Title;

    /// <summary>Gets or sets the clipboard that copy and paste use. Without it the copy and paste keys do nothing.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>Gets or sets the clock in milliseconds that tells a double press from two single ones. The default is the system tick count.</summary>
    public Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public override bool IsAnimating => buffer.CursorVisible && PseudoStates.Contains("focus") && GetValue(CursorBlinkProperty) > 0;

    /// <summary>Gets how many lines the view is scrolled back from the bottom. It is 0 when the latest output is shown.</summary>
    public int ScrollOffset => buffer.IsAlternateScreen ? 0 : scrollOffset;

    /// <summary>Gets whether some text is selected.</summary>
    public bool HasSelection => anchor is { } start && start != head;

    /// <summary>Gets the selected text, with the lines joined by line feeds and trailing spaces removed, or an empty text.</summary>
    public string SelectedText
    {
        get
        {
            if (anchor is not { } start || start == head)
                return string.Empty;

            var (from, to) = Ordered(start, head);
            var lines = new List<string>();

            for (var line = from.Line; line <= to.Line; line++)
            {
                var first = line == from.Line ? from.Column : 0;
                var last = line == to.Line ? to.Column : buffer.Columns - 1;
                var text = new StringBuilder();

                for (var column = first; column <= last; column++)
                    text.Append(CellAt(line, column).Character.ToString());

                lines.Add(text.ToString().TrimEnd(' '));
            }

            return string.Join('\n', lines);
        }
    }

    /// <summary>
    /// Connects a session: its output is written to the terminal, what is typed goes to it, size changes are passed on, and it is started
    /// with the current size. A session that was attached before is detached.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="dispatcher">
    /// Runs work on the thread that owns the terminal, since sessions write from other threads. For a window use
    /// <c>work =&gt; window.Post(work)</c>; without one the work runs on the thread that calls.
    /// </param>
    public void Attach(ITerminalSession session, Action<Action>? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        Detach();

        this.session = session;
        this.dispatcher = dispatcher;

        outputHandler = (_, e) => Run(() => Write(e.Data));
        exitedHandler = (_, e) => Run(() => Exited?.Invoke(this, e));

        session.Output += outputHandler;
        session.Exited += exitedHandler;

        session.Start(buffer.Columns, buffer.Rows);
    }

    /// <summary>Disconnects the session, if there is one. The session is not disposed.</summary>
    public void Detach()
    {
        if (session is null)
            return;

        session.Output -= outputHandler;
        session.Exited -= exitedHandler;

        session = null;
        dispatcher = null;
    }

    /// <summary>Gives the output of the program to the terminal.</summary>
    /// <param name="text">The text, with escape sequences.</param>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var before = buffer.ScrollbackCount;

        parser.Feed(text);

        KeepView(before);
    }

    /// <summary>Gives the output of the program to the terminal.</summary>
    /// <param name="data">The bytes of UTF-8 text, with escape sequences. They can end in the middle of a character.</param>
    public void Write(ReadOnlySpan<byte> data)
    {
        var before = buffer.ScrollbackCount;

        parser.Feed(data);

        KeepView(before);
    }

    /// <summary>Scrolls to the latest output.</summary>
    /// <returns><see langword="true"/> when the view moved.</returns>
    public bool ScrollToBottom()
    {
        var moved = scrollOffset != 0;

        scrollOffset = 0;

        return moved;
    }

    /// <summary>Scrolls the view through the scrollback.</summary>
    /// <param name="delta">The distance in pixels, which is positive towards the latest output. The engine's wheel step is 48 pixels.</param>
    /// <returns><see langword="true"/> when the view moved.</returns>
    public bool ScrollBy(double delta)
    {
        if (buffer.IsAlternateScreen || TextMeasurer is null)
            return false;

        var lines = (int)Math.Round(delta / CellSize().Height);

        if (lines == 0)
            lines = Math.Sign(delta);

        return ScrollLines(-lines);
    }

    /// <summary>Scrolls the view by lines.</summary>
    /// <param name="lines">The number of lines, positive towards older output.</param>
    /// <returns><see langword="true"/> when the view moved.</returns>
    public bool ScrollLines(int lines)
    {
        var before = scrollOffset;

        scrollOffset = Math.Clamp(scrollOffset + lines, 0, buffer.ScrollbackCount);

        return scrollOffset != before;
    }

    /// <summary>Selects everything: the scrollback and the screen.</summary>
    public void SelectAll()
    {
        anchor = (0, 0);
        head = (buffer.ScrollbackCount + buffer.Rows - 1, buffer.Columns - 1);
    }

    /// <summary>Removes the selection.</summary>
    public void ClearSelection() => anchor = null;

    /// <summary>Sends the selected text to the clipboard.</summary>
    /// <returns><see langword="true"/> when there was a selection and a clipboard.</returns>
    public bool Copy()
    {
        if (Clipboard is null || !HasSelection)
            return false;

        Clipboard.SetText(SelectedText);

        return true;
    }

    /// <summary>Sends the text on the clipboard to the program, wrapped in bracket markers when the program asked for them.</summary>
    /// <returns><see langword="true"/> when there was text to paste.</returns>
    public bool Paste()
    {
        var text = Clipboard?.GetText();

        if (string.IsNullOrEmpty(text))
            return false;

        text = text.Replace("\r\n", "\r").Replace('\n', '\r');

        if (buffer.BracketedPaste)
            text = "\u001b[200~" + text + "\u001b[201~";

        return Send(text);
    }

    /// <summary>Sends text to the program as if it was typed, and shows the latest output.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text was not empty.</returns>
    public bool Send(string text)
    {
        if (text.Length == 0)
            return false;

        scrollOffset = 0;
        lastInput = AnimationTime;

        Raise(text);

        return true;
    }

    /// <summary>Finds the text a key sends to the program.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys that are held.</param>
    /// <param name="applicationCursorKeys">Whether the program asked for application cursor keys, which changes what the arrows, Home and End send.</param>
    /// <returns>The text, or <see langword="null"/> when the key sends nothing. Printable characters arrive through text input instead.</returns>
    public static string? Encode(Key key, KeyModifiers modifiers, bool applicationCursorKeys)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var control = modifiers.HasFlag(KeyModifiers.Control);
        var parameter = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (control ? 4 : 0);
        var modified = parameter > 1;

        string Cursor(char final) => modified
            ? $"\u001b[1;{parameter}{final}"
            : applicationCursorKeys ? $"\u001bO{final}" : $"\u001b[{final}";

        string Function(char final) => modified ? $"\u001b[1;{parameter}{final}" : $"\u001bO{final}";

        string Tilde(int number) => modified ? $"\u001b[{number};{parameter}~" : $"\u001b[{number}~";

        var encoded = key switch
        {
            Key.Enter => "\r",
            Key.Backspace => "\u007f",
            Key.Tab => shift ? "\u001b[Z" : "\t",
            Key.Escape => "\u001b",
            Key.Up => Cursor('A'),
            Key.Down => Cursor('B'),
            Key.Right => Cursor('C'),
            Key.Left => Cursor('D'),
            Key.Home => Cursor('H'),
            Key.End => Cursor('F'),
            Key.Insert => Tilde(2),
            Key.Delete => Tilde(3),
            Key.PageUp => Tilde(5),
            Key.PageDown => Tilde(6),
            Key.F1 => Function('P'),
            Key.F2 => Function('Q'),
            Key.F3 => Function('R'),
            Key.F4 => Function('S'),
            Key.F5 => Tilde(15),
            Key.F6 => Tilde(17),
            Key.F7 => Tilde(18),
            Key.F8 => Tilde(19),
            Key.F9 => Tilde(20),
            Key.F10 => Tilde(21),
            Key.F11 => Tilde(23),
            Key.F12 => Tilde(24),

            _ => null,
        };

        if (encoded is not null)
            return alt && key is Key.Enter or Key.Backspace or Key.Tab or Key.Escape ? "\u001b" + encoded : encoded;

        if (key.Letter() is not { } letter)
            return null;

        if (control)
            return (alt ? "\u001b" : string.Empty) + (char)(letter - 'A' + 1);

        return alt ? "\u001b" + (shift ? letter : char.ToLowerInvariant(letter)) : null;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        var control = modifiers.HasFlag(KeyModifiers.Control);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);

        if (control && shift && key == Key.C || control && key == Key.Insert)
            return Copy();

        if (control && shift && key == Key.V || shift && !control && key == Key.Insert)
            return Paste();

        if (shift && !control && key is Key.PageUp or Key.PageDown && !buffer.IsAlternateScreen)
            return ScrollLines(key == Key.PageUp ? buffer.Rows - 1 : 1 - buffer.Rows);

        if (control && shift)
            return false;

        return Encode(key, modifiers, buffer.ApplicationCursorKeys) is { } text && Send(text);
    }

    /// <inheritdoc/>
    public override bool TextInput(string text) => Send(string.Concat(text.Where(character => !char.IsControl(character))));

    /// <inheritdoc/>
    public bool PointerDown(double x, double y)
    {
        var cell = CellAt(x, y);
        var now = Clock();
        var isDouble = lastPress is { } previous && now - previous <= DoublePressMilliseconds && cell == lastPressCell;

        lastPress = isDouble ? null : now;
        lastPressCell = cell;

        if (isDouble)
        {
            SelectWordAt(cell);

            dragging = false;

            return true;
        }

        anchor = cell;
        head = cell;
        dragging = true;

        return true;
    }

    /// <inheritdoc/>
    public bool PointerDrag(double x, double y)
    {
        if (!dragging)
            return false;

        head = CellAt(x, y);

        return true;
    }

    /// <inheritdoc/>
    public void PointerUp() => dragging = false;

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        if (TextMeasurer is null)
            return default;

        var cell = CellSize();

        return new Size(buffer.Columns * cell.Width, buffer.Rows * cell.Height);
    }

    /// <inheritdoc/>
    protected override bool PlaceChildren(Rect content)
    {
        if (TextMeasurer is null)
            return false;

        var cell = CellSize();
        var columns = Math.Max(1, (int)Math.Floor(content.Width / cell.Width));
        var rows = Math.Max(1, (int)Math.Floor(content.Height / cell.Height));

        if (columns == buffer.Columns && rows == buffer.Rows)
            return false;

        buffer.Resize(columns, rows);

        scrollOffset = Math.Min(scrollOffset, buffer.ScrollbackCount);

        Resized?.Invoke(this, new TerminalResizedEventArgs(columns, rows));
        session?.Resize(columns, rows);

        return false;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var background = GetValue(TerminalBackgroundProperty);

        context.FillRect(bounds, background, GetValue(BorderRadiusProperty));

        if (TextMeasurer is null)
            return;

        var content = ContentBox(bounds);
        var cell = CellSize();
        var palette = PaletteProperties.Select(property => GetValue(property)).ToArray();
        var offset = ScrollOffset;
        var top = buffer.ScrollbackCount - offset;
        var textTop = (cell.Height - Measurer().Measure("M", CellStyle(false, false)).Height) / 2;
        var selection = GetValue(SelectionColorProperty);

        context.Save();
        context.ClipRect(content);

        for (var row = 0; row < buffer.Rows; row++)
        {
            var y = content.Y + (row * cell.Height);
            var line = top + row;

            DrawBackgrounds(context, palette, line, content.X, y, cell);
            DrawSelection(context, selection, line, content.X, y, cell);
            DrawText(context, palette, line, content.X, y, textTop, cell);
        }

        if (offset == 0 && buffer.CursorVisible)
            DrawCursor(context, palette, content, cell, textTop);

        context.Restore();
    }

    /// <summary>Fills the cells that have a background of their own.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="palette">The 16 ANSI colors.</param>
    /// <param name="line">The absolute line.</param>
    /// <param name="left">The left edge of the first column.</param>
    /// <param name="y">The top of the row.</param>
    /// <param name="cell">The size of a cell.</param>
    void DrawBackgrounds(IDrawingContext context, Color[] palette, int line, double left, double y, Size cell)
    {
        var screen = GetValue(TerminalBackgroundProperty);

        for (var column = 0; column < buffer.Columns; column++)
        {
            var (_, background) = Colors(CellAt(line, column), palette);

            if (background == screen)
                continue;

            var start = column;

            while (column + 1 < buffer.Columns && Colors(CellAt(line, column + 1), palette).Background == background)
                column++;

            context.FillRect(new Rect(left + (start * cell.Width), y, (column - start + 1) * cell.Width, cell.Height), background);
        }
    }

    /// <summary>Covers the selected cells of a row.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="color">The selection color.</param>
    /// <param name="line">The absolute line.</param>
    /// <param name="left">The left edge of the first column.</param>
    /// <param name="y">The top of the row.</param>
    /// <param name="cell">The size of a cell.</param>
    void DrawSelection(IDrawingContext context, Color color, int line, double left, double y, Size cell)
    {
        if (anchor is not { } start || start == head)
            return;

        var (from, to) = Ordered(start, head);

        if (line < from.Line || line > to.Line)
            return;

        var first = line == from.Line ? from.Column : 0;
        var last = line == to.Line ? to.Column : buffer.Columns - 1;

        context.FillRect(new Rect(left + (first * cell.Width), y, (last - first + 1) * cell.Width, cell.Height), color);
    }

    /// <summary>Draws the text of a row in runs of cells that share their style.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="palette">The 16 ANSI colors.</param>
    /// <param name="line">The absolute line.</param>
    /// <param name="left">The left edge of the first column.</param>
    /// <param name="y">The top of the row.</param>
    /// <param name="textTop">The gap above the text inside a cell.</param>
    /// <param name="cell">The size of a cell.</param>
    void DrawText(IDrawingContext context, Color[] palette, int line, double left, double y, double textTop, Size cell)
    {
        for (var column = 0; column < buffer.Columns; column++)
        {
            var first = CellAt(line, column);
            var (color, _) = Colors(first, palette);
            var style = first.Attributes & ~(TerminalAttributes.Blink | TerminalAttributes.Reverse | TerminalAttributes.Faint);
            var start = column;
            var text = new StringBuilder();

            for (; column < buffer.Columns; column++)
            {
                var next = CellAt(line, column);

                if (Colors(next, palette).Foreground != color || (next.Attributes & ~(TerminalAttributes.Blink | TerminalAttributes.Reverse | TerminalAttributes.Faint)) != style)
                    break;

                text.Append(next.Attributes.HasFlag(TerminalAttributes.Hidden) ? " " : next.Character.ToString());
            }

            column--;

            var x = left + (start * cell.Width);
            var width = (column - start + 1) * cell.Width;
            var content = text.ToString();

            if (content.Trim(' ').Length > 0)
                context.DrawText(content.TrimEnd(' '), x, y + textTop, CellStyle(style.HasFlag(TerminalAttributes.Bold), style.HasFlag(TerminalAttributes.Italic)), color);

            if (style.HasFlag(TerminalAttributes.Underline))
                context.FillRect(new Rect(x, y + cell.Height - 2, width, 1), color);

            if (style.HasFlag(TerminalAttributes.Strikethrough))
                context.FillRect(new Rect(x, y + (cell.Height / 2), width, 1), color);
        }
    }

    /// <summary>Draws the cursor, which is hollow while the terminal has no focus and blinks while it has.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="palette">The 16 ANSI colors.</param>
    /// <param name="content">The content box.</param>
    /// <param name="cell">The size of a cell.</param>
    /// <param name="textTop">The gap above the text inside a cell.</param>
    void DrawCursor(IDrawingContext context, Color[] palette, Rect content, Size cell, double textTop)
    {
        var focused = PseudoStates.Contains("focus");
        var period = GetValue(CursorBlinkProperty);

        if (focused && period > 0 && ((AnimationTime - lastInput).TotalMilliseconds / period) % 2 >= 1)
            return;

        var rect = new Rect(content.X + (buffer.CursorColumn * cell.Width), content.Y + (buffer.CursorRow * cell.Height), cell.Width, cell.Height);
        var color = GetValue(CursorColorProperty);

        if (!focused)
        {
            context.StrokeRect(rect, color, 1);

            return;
        }

        switch (GetValue(CursorStyleProperty))
        {
            case "bar":
                context.FillRect(new Rect(rect.X, rect.Y, 2, rect.Height), color);
                break;

            case "underline":
                context.FillRect(new Rect(rect.X, rect.Bottom - 2, rect.Width, 2), color);
                break;

            default:
                var under = buffer.GetCell(buffer.CursorColumn, buffer.CursorRow);

                context.FillRect(rect, color);

                if (!under.IsBlank)
                    context.DrawText(under.Character.ToString(), rect.X, rect.Y + textTop,
                        CellStyle(under.Attributes.HasFlag(TerminalAttributes.Bold), under.Attributes.HasFlag(TerminalAttributes.Italic)),
                        GetValue(TerminalBackgroundProperty));

                break;
        }
    }

    /// <summary>Gets the size of a character cell.</summary>
    /// <returns>The width of <c>M</c> and the font height times <c>terminal-line-height</c>, rounded.</returns>
    Size CellSize()
    {
        var size = Measurer().Measure("M", CellStyle(false, false));

        return new Size(Math.Max(1, size.Width), Math.Max(1, Math.Round(size.Height * Math.Max(0.5, GetValue(LineHeightProperty)))));
    }

    /// <summary>Builds the font of the terminal.</summary>
    /// <param name="bold">Whether the text is bold.</param>
    /// <param name="italic">Whether the text is italic.</param>
    TextStyle CellStyle(bool bold, bool italic) => new(GetValue(FontFamilyProperty), GetValue(FontSizeProperty), bold, italic);

    /// <summary>Resolves the colors of a cell: bold brightens the first eight colors, reverse swaps the two colors and faint fades the text.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="palette">The 16 ANSI colors.</param>
    /// <returns>The text color and the background color to draw.</returns>
    (Color Foreground, Color Background) Colors(TerminalCell cell, Color[] palette)
    {
        var foreground = cell.Foreground;

        if (cell.Attributes.HasFlag(TerminalAttributes.Bold) && foreground is { Kind: TerminalColorKind.Indexed, Index: < 8 })
            foreground = TerminalColor.FromIndex((byte)(foreground.Index + 8));

        var text = Resolve(foreground, palette, GetValue(TerminalForegroundProperty));
        var screen = Resolve(cell.Background, palette, GetValue(TerminalBackgroundProperty));

        if (cell.Attributes.HasFlag(TerminalAttributes.Reverse))
            (text, screen) = (screen, text);

        if (cell.Attributes.HasFlag(TerminalAttributes.Faint))
            text = new Color((byte)((text.R + screen.R) / 2), (byte)((text.G + screen.G) / 2), (byte)((text.B + screen.B) / 2), text.A);

        return (text, screen);
    }

    /// <summary>Turns a terminal color into a real color.</summary>
    /// <param name="color">The terminal color.</param>
    /// <param name="palette">The 16 ANSI colors.</param>
    /// <param name="fallback">The color for the default.</param>
    static Color Resolve(TerminalColor color, Color[] palette, Color fallback)
    {
        switch (color.Kind)
        {
            case TerminalColorKind.Rgb:
                return new Color(color.R, color.G, color.B);

            case TerminalColorKind.Indexed when color.Index < 16:
                return palette[color.Index];

            case TerminalColorKind.Indexed when color.Index < 232:
                var cube = color.Index - 16;

                return new Color(CubeLevels[cube / 36], CubeLevels[cube / 6 % 6], CubeLevels[cube % 6]);

            case TerminalColorKind.Indexed:
                var grey = (byte)(8 + ((color.Index - 232) * 10));

                return new Color(grey, grey, grey);

            default:
                return fallback;
        }
    }

    /// <summary>Gets a cell by absolute line, where the scrollback comes first and the screen after it.</summary>
    /// <param name="line">The line from the oldest scrollback line.</param>
    /// <param name="column">The column.</param>
    /// <returns>The cell, or a blank when the line is shorter.</returns>
    TerminalCell CellAt(int line, int column)
    {
        if (line < 0 || column < 0)
            return TerminalCell.Blank;

        if (line >= buffer.ScrollbackCount)
        {
            var row = line - buffer.ScrollbackCount;

            return row < buffer.Rows && column < buffer.Columns ? buffer.GetCell(column, row) : TerminalCell.Blank;
        }

        var cells = buffer.GetScrollbackRow(line);

        return column < cells.Count ? cells[column] : TerminalCell.Blank;
    }

    /// <summary>Finds the cell at a position, as an absolute line and a column, clamped to the screen.</summary>
    /// <param name="x">The horizontal position relative to the border box.</param>
    /// <param name="y">The vertical position relative to the border box.</param>
    (int Line, int Column) CellAt(double x, double y)
    {
        var content = ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));
        var cell = CellSize();
        var column = Math.Clamp((int)Math.Floor((x - content.X) / cell.Width), 0, buffer.Columns - 1);
        var row = Math.Clamp((int)Math.Floor((y - content.Y) / cell.Height), 0, buffer.Rows - 1);

        return (buffer.ScrollbackCount - ScrollOffset + row, column);
    }

    /// <summary>Selects the word around a cell.</summary>
    /// <param name="cell">The cell.</param>
    void SelectWordAt((int Line, int Column) cell)
    {
        var (line, column) = cell;

        if (!IsWordCharacter(CellAt(line, column)))
        {
            anchor = cell;
            head = cell;

            return;
        }

        var first = column;
        var last = column;

        while (first > 0 && IsWordCharacter(CellAt(line, first - 1)))
            first--;

        while (last < buffer.Columns - 1 && IsWordCharacter(CellAt(line, last + 1)))
            last++;

        anchor = (line, first);
        head = (line, last);
    }

    /// <summary>Tells whether a cell belongs to a word: anything that is not a space or a bracket, quote or separator.</summary>
    /// <param name="cell">The cell.</param>
    static bool IsWordCharacter(TerminalCell cell) => !cell.IsBlank && !"\"'`()[]{}<>|,;".Contains(cell.Character.ToString());

    /// <summary>Puts two cells in reading order.</summary>
    static ((int Line, int Column) From, (int Line, int Column) To) Ordered((int Line, int Column) a, (int Line, int Column) b) =>
        a.Line < b.Line || (a.Line == b.Line && a.Column <= b.Column) ? (a, b) : (b, a);

    /// <summary>Sends text to the program: raises <see cref="Input"/> and gives it to the session.</summary>
    /// <param name="text">The text.</param>
    void Raise(string text)
    {
        Input?.Invoke(this, new TerminalInputEventArgs(text));
        session?.Send(text);
    }

    /// <summary>Runs work on the thread of the terminal, through the dispatcher when there is one.</summary>
    /// <param name="work">The work.</param>
    void Run(Action work)
    {
        if (dispatcher is { } post)
            post(work);
        else
            work();
    }

    /// <summary>Keeps the view still while new lines go to the scrollback, if the user scrolled back.</summary>
    /// <param name="before">The scrollback count before the output.</param>
    void KeepView(int before)
    {
        if (scrollOffset > 0)
            scrollOffset = Math.Min(scrollOffset + Math.Max(0, buffer.ScrollbackCount - before), buffer.ScrollbackCount);
    }
}
