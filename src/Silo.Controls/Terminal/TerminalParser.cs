using System.Globalization;
using System.Text;

namespace Silo;

/// <summary>
/// Reads the output of a program, which is text mixed with ANSI and VT100 escape sequences, and applies it to a
/// <see cref="TerminalBuffer"/>.
/// </summary>
/// <remarks>
/// <para>
/// It understands the sequences that command line programs and full-screen programs such as shells, <c>ls --color</c>, editors and
/// pagers use: cursor movement and positioning, erasing, inserting and deleting lines and characters, scrolling regions, SGR styles
/// with 16, 256 and true colors, the alternate screen, the DEC line-drawing character set, cursor and wrap modes, bracketed paste,
/// the window title (OSC 0 and 2), and the status reports a program asks for, which go to <see cref="Reply"/>. Anything else is
/// read and ignored, so unknown sequences never leak onto the screen.
/// </para>
/// <para>
/// Data can arrive in any pieces: a sequence or a UTF-8 character may be cut between two calls to <see cref="Feed(ReadOnlySpan{byte})"/>
/// and is finished by the next one.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var buffer = new TerminalBuffer(80, 24);
/// var parser = new TerminalParser(buffer);
///
/// parser.Feed("\u001b[1;31mError:\u001b[0m disk full\r\n");
/// </code>
/// </example>
public sealed class TerminalParser
{
    /// <summary>The longest string (title or other operating system command) that is kept.</summary>
    const int MaxStringLength = 4096;

    /// <summary>The most parameters of one control sequence that are kept.</summary>
    const int MaxParameters = 32;

    /// <summary>The DEC line-drawing characters, from <c>`</c> (0x60) to <c>~</c> (0x7E).</summary>
    const string LineDrawing = "◆▒␉␌␍␊°±␤␋┘┐┌└┼⎺⎻─⎼⎽├┤┴┬│≤≥π≠£·";

    readonly TerminalBuffer buffer;

    readonly Decoder decoder = new UTF8Encoding(false).GetDecoder();

    readonly List<int> parameters = [];

    readonly StringBuilder text = new();

    State state = State.Ground;

    char highSurrogate;

    bool hasParameter;

    int parameter;

    char privateMarker;

    char intermediate;

    char designating;

    bool lineDrawing;

    /// <summary>Creates a parser that writes to a buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    public TerminalParser(TerminalBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        this.buffer = buffer;
    }

    /// <summary>Raised when the program rings the bell (BEL, 0x07).</summary>
    public event EventHandler? Bell;

    /// <summary>
    /// Gets or sets the method that receives the answers that the program asked for, such as the cursor position, which have to be
    /// sent back to the program as if they were typed.
    /// </summary>
    public Action<string>? Reply { get; set; }

    /// <summary>The states of the parser.</summary>
    enum State
    {
        Ground,
        Escape,
        EscapeIntermediate,
        Csi,
        Osc,
        IgnoredString,
    }

    /// <summary>Reads bytes of UTF-8 text from the program.</summary>
    /// <param name="data">The bytes. They can end in the middle of a character.</param>
    public void Feed(ReadOnlySpan<byte> data)
    {
        var chars = new char[decoder.GetCharCount(data, flush: false)];
        var count = decoder.GetChars(data, chars, flush: false);

        Feed(chars.AsSpan(0, count));
    }

    /// <summary>Reads text from the program.</summary>
    /// <param name="data">The text.</param>
    public void Feed(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Feed(data.AsSpan());
    }

    /// <summary>Reads text from the program.</summary>
    /// <param name="data">The text. A surrogate pair can be split between two calls.</param>
    public void Feed(ReadOnlySpan<char> data)
    {
        foreach (var character in data)
        {
            if (char.IsHighSurrogate(character))
            {
                highSurrogate = character;

                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                if (highSurrogate != '\0' && Rune.TryCreate(highSurrogate, character, out var pair))
                    Process(pair.Value);

                highSurrogate = '\0';

                continue;
            }

            highSurrogate = '\0';

            Process(character);
        }
    }

    /// <summary>Handles one code point.</summary>
    /// <param name="code">The code point.</param>
    void Process(int code)
    {
        if (code == 0x18 || code == 0x1A)
        {
            state = State.Ground;

            return;
        }

        if (code == 0x1B)
        {
            if (state == State.Osc)
                FinishString();

            state = State.Escape;
            intermediate = '\0';

            return;
        }

        if (state is State.Osc or State.IgnoredString)
        {
            ProcessString(code);

            return;
        }

        if (code < 0x20 || code == 0x7F)
        {
            Execute(code);

            return;
        }

        switch (state)
        {
            case State.Ground:
                Print(code);
                break;

            case State.Escape:
                ProcessEscape((char)code);
                break;

            case State.EscapeIntermediate:
                FinishDesignation((char)code);
                break;

            case State.Csi:
                ProcessCsi(code);
                break;
        }
    }

    /// <summary>Runs a control character.</summary>
    /// <param name="code">The character, below 0x20 or DEL.</param>
    void Execute(int code)
    {
        switch (code)
        {
            case 0x07:
                Bell?.Invoke(this, EventArgs.Empty);
                break;

            case 0x08:
                buffer.Backspace();
                break;

            case 0x09:
                buffer.Tab();
                break;

            case 0x0A or 0x0B or 0x0C:
                buffer.LineFeed();
                break;

            case 0x0D:
                buffer.CarriageReturn();
                break;
        }
    }

    /// <summary>Writes a printable character, through the line-drawing set when it is on.</summary>
    /// <param name="code">The code point.</param>
    void Print(int code)
    {
        if (lineDrawing && code is >= 0x60 and <= 0x7E)
            code = LineDrawing[code - 0x60];

        if (Rune.IsValid(code) && !IsCombining(code))
            buffer.Write(new Rune(code));
    }

    /// <summary>Tells whether a code point is a combining mark, which takes no cell of its own.</summary>
    /// <param name="code">The code point.</param>
    static bool IsCombining(int code) =>
        Rune.GetUnicodeCategory(new Rune(code)) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark;

    /// <summary>Handles the character after ESC.</summary>
    /// <param name="character">The character.</param>
    void ProcessEscape(char character)
    {
        state = State.Ground;

        switch (character)
        {
            case '[':
                BeginCsi();
                break;

            case ']':
                text.Clear();
                state = State.Osc;
                break;

            case 'P' or 'X' or '^' or '_':
                state = State.IgnoredString;
                break;

            case '(' or ')' or '*' or '+':
                designating = character;
                state = State.EscapeIntermediate;
                break;

            case '#' or '%' or ' ':
                designating = '\0';
                state = State.EscapeIntermediate;
                break;

            case '7':
                buffer.SaveCursor();
                break;

            case '8':
                buffer.RestoreCursor();
                break;

            case 'D':
                buffer.LineFeed();
                break;

            case 'E':
                buffer.CarriageReturn();
                buffer.LineFeed();
                break;

            case 'M':
                buffer.ReverseIndex();
                break;

            case 'c':
                buffer.Reset();

                lineDrawing = false;

                break;
        }
    }

    /// <summary>Finishes ESC followed by a character set designation or another one-character sequence.</summary>
    /// <param name="character">The final character.</param>
    void FinishDesignation(char character)
    {
        state = State.Ground;

        if (designating == '(')
            lineDrawing = character == '0';
    }

    /// <summary>Starts a control sequence.</summary>
    void BeginCsi()
    {
        parameters.Clear();

        hasParameter = false;
        parameter = 0;
        privateMarker = '\0';
        intermediate = '\0';
        state = State.Csi;
    }

    /// <summary>Handles one character of a control sequence.</summary>
    /// <param name="code">The code point.</param>
    void ProcessCsi(int code)
    {
        switch (code)
        {
            case >= '0' and <= '9':
                parameter = Math.Min(parameter * 10 + (code - '0'), 100000);
                hasParameter = true;
                break;

            case ';' or ':':
                PushParameter();
                break;

            case >= '<' and <= '?':
                privateMarker = (char)code;
                break;

            case >= 0x20 and <= 0x2F:
                intermediate = (char)code;
                break;

            case >= 0x40 and <= 0x7E:
                PushParameter(last: true);

                state = State.Ground;

                Dispatch((char)code);
                break;

            default:
                state = State.Ground;
                break;
        }
    }

    /// <summary>Ends the parameter being read.</summary>
    /// <param name="last"><see langword="true"/> at the end of the sequence, where an empty last parameter is only kept after a separator.</param>
    void PushParameter(bool last = false)
    {
        if (last && !hasParameter && parameters.Count == 0)
            return;

        if (parameters.Count < MaxParameters)
            parameters.Add(hasParameter ? parameter : -1);

        parameter = 0;
        hasParameter = false;
    }

    /// <summary>Handles a character inside an operating system command or an ignored string.</summary>
    /// <param name="code">The code point.</param>
    void ProcessString(int code)
    {
        if (code == 0x07)
        {
            FinishString();

            state = State.Ground;

            return;
        }

        if (state == State.Osc && text.Length < MaxStringLength && code >= 0x20)
            text.Append(char.ConvertFromUtf32(code));
    }

    /// <summary>Runs the operating system command that was collected.</summary>
    void FinishString()
    {
        if (state != State.Osc)
            return;

        var content = text.ToString();
        var split = content.IndexOf(';');

        if (split > 0 && content[..split] is "0" or "2")
            buffer.Title = content[(split + 1)..];

        text.Clear();
    }

    /// <summary>Gets a parameter of the control sequence.</summary>
    /// <param name="index">The index.</param>
    /// <param name="fallback">The value when it is missing or empty.</param>
    int Parameter(int index, int fallback) => index < parameters.Count && parameters[index] >= 0 ? parameters[index] : fallback;

    /// <summary>Gets a count parameter, where 0 and a missing value both mean 1.</summary>
    /// <param name="index">The index.</param>
    int Count(int index) => Math.Max(1, Parameter(index, 1));

    /// <summary>Runs a control sequence.</summary>
    /// <param name="final">The final character.</param>
    void Dispatch(char final)
    {
        if (privateMarker == '?' && final is 'h' or 'l')
        {
            SetPrivateModes(final == 'h');

            return;
        }

        if (privateMarker != '\0' || intermediate != '\0')
        {
            if (privateMarker == '>' && final == 'c')
                Reply?.Invoke("\u001b[>0;0;0c");

            return;
        }

        switch (final)
        {
            case 'A':
                buffer.CursorUp(Count(0));
                break;

            case 'B' or 'e':
                buffer.CursorDown(Count(0));
                break;

            case 'C' or 'a':
                buffer.CursorForward(Count(0));
                break;

            case 'D':
                buffer.CursorBack(Count(0));
                break;

            case 'E':
                buffer.CursorDown(Count(0));
                buffer.CarriageReturn();
                break;

            case 'F':
                buffer.CursorUp(Count(0));
                buffer.CarriageReturn();
                break;

            case 'G' or '`':
                buffer.MoveTo(buffer.CursorRow, Count(0) - 1);
                break;

            case 'H' or 'f':
                buffer.MoveTo(Count(0) - 1, Count(1) - 1);
                break;

            case 'd':
                buffer.MoveTo(Count(0) - 1, buffer.CursorColumn);
                break;

            case 'J':
                buffer.EraseInDisplay(Parameter(0, 0));
                break;

            case 'K':
                buffer.EraseInLine(Parameter(0, 0));
                break;

            case 'L':
                buffer.InsertLines(Count(0));
                break;

            case 'M':
                buffer.DeleteLines(Count(0));
                break;

            case '@':
                buffer.InsertCharacters(Count(0));
                break;

            case 'P':
                buffer.DeleteCharacters(Count(0));
                break;

            case 'X':
                buffer.EraseCharacters(Count(0));
                break;

            case 'S':
                buffer.ScrollUp(Count(0));
                break;

            case 'T':
                buffer.ScrollDown(Count(0));
                break;

            case 'r':
                buffer.SetScrollRegion(Count(0) - 1, Parameter(1, buffer.Rows) - 1);
                break;

            case 's':
                buffer.SaveCursor();
                break;

            case 'u':
                buffer.RestoreCursor();
                break;

            case 'm':
                ApplyGraphics();
                break;

            case 'n':
                Report(Parameter(0, 0));
                break;

            case 'c' when Parameter(0, 0) == 0:
                Reply?.Invoke("\u001b[?1;2c");
                break;
        }
    }

    /// <summary>Turns the modes of a <c>CSI ? ... h</c> or <c>CSI ? ... l</c> sequence on or off.</summary>
    /// <param name="on"><see langword="true"/> for <c>h</c>.</param>
    void SetPrivateModes(bool on)
    {
        foreach (var mode in parameters)
        {
            switch (mode)
            {
                case 1:
                    buffer.ApplicationCursorKeys = on;
                    break;

                case 7:
                    buffer.AutoWrap = on;
                    break;

                case 25:
                    buffer.CursorVisible = on;
                    break;

                case 47 or 1047:
                    buffer.UseAlternateScreen(on);
                    break;

                case 1049:
                    if (on)
                        buffer.SaveCursor();

                    buffer.UseAlternateScreen(on);

                    if (!on)
                        buffer.RestoreCursor();

                    break;

                case 2004:
                    buffer.BracketedPaste = on;
                    break;
            }
        }
    }

    /// <summary>Answers a device status report request.</summary>
    /// <param name="kind">5 asks if the terminal is fine, 6 asks for the cursor position.</param>
    void Report(int kind)
    {
        if (kind == 5)
            Reply?.Invoke("\u001b[0n");
        else if (kind == 6)
            Reply?.Invoke($"\u001b[{buffer.CursorRow + 1};{buffer.CursorColumn + 1}R");
    }

    /// <summary>Applies an SGR sequence (<c>CSI ... m</c>) to the pen.</summary>
    void ApplyGraphics()
    {
        if (parameters.Count == 0)
        {
            buffer.ResetPen();

            return;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var code = Parameter(i, 0);

            switch (code)
            {
                case 0:
                    buffer.ResetPen();
                    break;

                case 1:
                    buffer.Attributes |= TerminalAttributes.Bold;
                    break;

                case 2:
                    buffer.Attributes |= TerminalAttributes.Faint;
                    break;

                case 3:
                    buffer.Attributes |= TerminalAttributes.Italic;
                    break;

                case 4:
                    buffer.Attributes |= TerminalAttributes.Underline;
                    break;

                case 5 or 6:
                    buffer.Attributes |= TerminalAttributes.Blink;
                    break;

                case 7:
                    buffer.Attributes |= TerminalAttributes.Reverse;
                    break;

                case 8:
                    buffer.Attributes |= TerminalAttributes.Hidden;
                    break;

                case 9:
                    buffer.Attributes |= TerminalAttributes.Strikethrough;
                    break;

                case 22:
                    buffer.Attributes &= ~(TerminalAttributes.Bold | TerminalAttributes.Faint);
                    break;

                case 23:
                    buffer.Attributes &= ~TerminalAttributes.Italic;
                    break;

                case 24:
                    buffer.Attributes &= ~TerminalAttributes.Underline;
                    break;

                case 25:
                    buffer.Attributes &= ~TerminalAttributes.Blink;
                    break;

                case 27:
                    buffer.Attributes &= ~TerminalAttributes.Reverse;
                    break;

                case 28:
                    buffer.Attributes &= ~TerminalAttributes.Hidden;
                    break;

                case 29:
                    buffer.Attributes &= ~TerminalAttributes.Strikethrough;
                    break;

                case >= 30 and <= 37:
                    buffer.Foreground = TerminalColor.FromIndex((byte)(code - 30));
                    break;

                case 38 or 48:
                    ApplyExtendedColor(code == 38, ref i);
                    break;

                case 39:
                    buffer.Foreground = default;
                    break;

                case >= 40 and <= 47:
                    buffer.Background = TerminalColor.FromIndex((byte)(code - 40));
                    break;

                case 49:
                    buffer.Background = default;
                    break;

                case >= 90 and <= 97:
                    buffer.Foreground = TerminalColor.FromIndex((byte)(code - 90 + 8));
                    break;

                case >= 100 and <= 107:
                    buffer.Background = TerminalColor.FromIndex((byte)(code - 100 + 8));
                    break;
            }
        }
    }

    /// <summary>Reads the <c>5;n</c> or <c>2;r;g;b</c> that follows SGR 38 or 48.</summary>
    /// <param name="foreground"><see langword="true"/> for 38, the text color.</param>
    /// <param name="index">The index of the 38 or 48 parameter, moved to the last parameter that was used.</param>
    void ApplyExtendedColor(bool foreground, ref int index)
    {
        var mode = Parameter(index + 1, 0);
        TerminalColor? color = null;

        if (mode == 5)
        {
            color = TerminalColor.FromIndex((byte)Math.Clamp(Parameter(index + 2, 0), 0, 255));
            index += 2;
        }
        else if (mode == 2)
        {
            color = TerminalColor.FromRgb(Channel(index + 2), Channel(index + 3), Channel(index + 4));
            index += 4;
        }

        if (color is not { } value)
            return;

        if (foreground)
            buffer.Foreground = value;
        else
            buffer.Background = value;
    }

    /// <summary>Gets a color channel from a parameter, clamped to a byte.</summary>
    /// <param name="index">The index.</param>
    byte Channel(int index) => (byte)Math.Clamp(Parameter(index, 0), 0, 255);
}
