using System.Globalization;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A multi-line text editor: a caret, a selection, scrolling, undo and the usual editing keys, with optional line numbers.
/// It shows a <see cref="TextBuffer"/>, which holds the text and does the editing.
/// </summary>
/// <remarks>
/// <para>
/// It uses one font for the whole text, so give it a monospace <c>font-family</c> to line up columns. It has the
/// <c>selection-color</c>, <c>caret-color</c> and <c>gutter-color</c> properties, and the Background and Color of any Control.
/// Set <see cref="ShowLineNumbers"/> to show the line numbers in a column on the left.
/// </para>
/// <para>
/// Copy, cut and paste work when <see cref="Clipboard"/> is set. Without it the keys do nothing.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var editor = new TextArea(new TextBuffer(File.ReadAllText(path))) { ShowLineNumbers = true };
///
/// editor.TextChanged += (_, _) => Console.WriteLine(editor.Buffer.Text.Length);
/// </code>
/// </example>
[PseudoStates("hover", "focus")]
public class TextArea : Control, IScrollable, IPointerHandler
{
    /// <summary>The space on each side of the line numbers, in pixels.</summary>
    const double GutterPadding = 8;

    /// <summary>The space between the line numbers and the text, in pixels.</summary>
    const double TextPadding = 6;

    /// <summary>The smallest width when no <c>width</c> is set, in pixels.</summary>
    const double MinimumWidth = 240;

    /// <summary>The smallest height when no <c>height</c> is set, in lines.</summary>
    const int MinimumLines = 8;

    /// <summary>The color of the selected text's background.</summary>
    public static readonly StyledProperty<Color> SelectionColorProperty =
        StyledProperty.Register<TextArea, Color>("selection-color", new(59, 130, 246, 90), Color.TryParse);

    /// <summary>The color of the caret.</summary>
    public static readonly StyledProperty<Color> CaretColorProperty =
        StyledProperty.Register<TextArea, Color>("caret-color", new(0, 0, 0), Color.TryParse);

    /// <summary>The color of the line numbers.</summary>
    public static readonly StyledProperty<Color> GutterColorProperty =
        StyledProperty.Register<TextArea, Color>("gutter-color", new(156, 163, 175), Color.TryParse);


    /// <summary>The text and what is done to it.</summary>
    readonly TextBuffer buffer;

    /// <summary>The text that was last reported by <see cref="TextChanged"/>.</summary>
    string reported;

    /// <summary>How far the lines are scrolled up.</summary>
    readonly ScrollState scroll;

    /// <summary>How far the text is scrolled to the left, in pixels.</summary>
    double scrollX;

    /// <summary>Whether the next frame must scroll the caret into view.</summary>
    bool revealCaret = true;

    /// <summary>The height of a line when the Control was last drawn.</summary>
    double lineHeight = 16;

    /// <summary>The height of the text area when the Control was last drawn.</summary>
    double viewportHeight;

    /// <summary>Whether the primary button is held after a press in the text.</summary>
    bool selecting;


    /// <summary>
    /// Runs before the first TextArea is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TextArea()
    {
    }

    /// <summary>Creates a text area.</summary>
    /// <param name="buffer">The buffer to show, or <see langword="null"/> for an empty one.</param>
    public TextArea(TextBuffer? buffer = null)
    {
        this.buffer = buffer ?? new TextBuffer();

        scroll = new ScrollState(() => this.buffer.LineCount * lineHeight, () => viewportHeight);

        reported = this.buffer.Text;

        this.buffer.Changed += (_, _) => OnBufferChanged();
    }

    /// <summary>Raised after the text changed, but not after the caret or the selection moved.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Gets the buffer that the editor shows.</summary>
    public TextBuffer Buffer => buffer;

    /// <summary>Gets or sets what copy, cut and paste use. Without it those keys do nothing.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>Gets or sets whether a column with the line numbers is shown left of the text. The default is <see langword="false"/>.</summary>
    public bool ShowLineNumbers { get; set; }

    /// <summary>
    /// Gets or sets whether typing, Enter, Tab, Backspace, Delete, cut, paste, undo and redo are ignored. Moving, selecting and copying
    /// still work, and the text can still be changed from code through <see cref="Buffer"/>. The default is <see langword="false"/>.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public bool ScrollBy(double delta) => scroll.ScrollBy(delta);

    /// <inheritdoc/>
    public bool PointerDown(PointerEvent e)
    {
        selecting = true;

        revealCaret = true;

        buffer.MoveTo(OffsetAt(e.X, e.Y), extend: false);

        return true;
    }

    /// <inheritdoc/>
    public bool PointerDrag(PointerEvent e)
    {
        if (!selecting)
            return false;

        revealCaret = true;

        buffer.MoveTo(OffsetAt(e.X, e.Y), extend: true);

        return true;
    }

    /// <inheritdoc/>
    public bool PointerUp(PointerEvent e)
    {
        selecting = false;

        return false;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var control = modifiers.HasFlag(KeyModifiers.Control);

        revealCaret = true;

        if (IsReadOnly && IsEdit(key, control))
        {
            if (key == Key.X)
                Copy();

            return key == Key.X;
        }

        switch (key)
        {
            case Key.Left:
                buffer.MoveLeft(shift);
                break;

            case Key.Right:
                buffer.MoveRight(shift);
                break;

            case Key.Up:
                buffer.MoveUp(shift);
                break;

            case Key.Down:
                buffer.MoveDown(shift);
                break;

            case Key.Home when control:
                buffer.MoveTo(0, shift);
                break;

            case Key.End when control:
                buffer.MoveToEnd(shift);
                break;

            case Key.Home:
                buffer.MoveToLineStart(shift);
                break;

            case Key.End:
                buffer.MoveToLineEnd(shift);
                break;

            case Key.PageUp:
                MovePage(-1, shift);
                break;

            case Key.PageDown:
                MovePage(1, shift);
                break;

            case Key.Enter:
                buffer.NewLine();
                break;

            case Key.Backspace:
                buffer.Backspace();
                break;

            case Key.Delete:
                buffer.Delete();
                break;

            case Key.Tab when shift:
                buffer.Untab();
                break;

            case Key.Tab:
                buffer.Tab();
                break;

            case Key.A when control:
                buffer.SelectAll();
                break;

            case Key.Z when control && shift:
            case Key.Y when control:
                buffer.Redo();
                break;

            case Key.Z when control:
                buffer.Undo();
                break;

            case Key.C when control:
                Copy();
                break;

            case Key.X when control:
                Copy();
                buffer.Delete();
                break;

            case Key.V when control:
                buffer.Insert(Clipboard?.GetText() ?? string.Empty);
                break;

            default:
                return false;
        }

        return true;
    }

    /// <summary>Tells whether a key changes the text, which a read-only text area ignores.</summary>
    /// <param name="key">The key.</param>
    /// <param name="control">Whether Control is held.</param>
    /// <returns><see langword="true"/> for Enter, Tab, Backspace, Delete, and Control with X, V, Z or Y.</returns>
    static bool IsEdit(Key key, bool control)
        => key is Key.Enter or Key.Tab or Key.Backspace or Key.Delete || (control && key is Key.X or Key.V or Key.Z or Key.Y);

    /// <inheritdoc/>
    public override bool TextInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var clean = string.Concat(input.Where(c => !char.IsControl(c)));

        if (clean.Length == 0 || IsReadOnly)
            return false;

        revealCaret = true;

        buffer.Insert(clean);

        return true;
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        return new Size(MinimumWidth, Measurer().LineHeight(CurrentTextStyle()) * MinimumLines);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var height = measurer.LineHeight(style);
        var gutter = GutterWidth(measurer, style);
        var text = new Rect(content.X + gutter, content.Y, Math.Max(0, content.Width - gutter), content.Height);

        lineHeight = height;
        viewportHeight = content.Height;

        if (revealCaret)
            Reveal(measurer, style, text);

        scroll.Clamp();

        context.Save();
        context.ClipRect(content);

        DrawLines(context, measurer, style, content, text, gutter);

        context.Restore();
    }

    /// <summary>Draws the selection, the text, the line numbers and the caret.</summary>
    /// <param name="context">Where to draw.</param>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="style">The font.</param>
    /// <param name="content">The content box.</param>
    /// <param name="text">The area of the text, right of the line numbers.</param>
    /// <param name="gutter">The width of the line numbers' column.</param>
    void DrawLines(
        IDrawingContext context,
        ITextMeasurer measurer,
        TextStyle style,
        Rect content,
        Rect text,
        double gutter)
    {
        var first = Math.Max(0, (int)(scroll.Offset / lineHeight));
        var last = Math.Min(buffer.LineCount - 1, (int)((scroll.Offset + content.Height) / lineHeight));
        var color = GetValue(ColorProperty);
        var gutterColor = GetValue(GutterColorProperty);
        var focused = PseudoStates.Contains("focus");

        for (var line = first; line <= last; line++)
        {
            var y = content.Y + (line * lineHeight) - scroll.Offset;

            if (ShowLineNumbers)
            {
                var number = (line + 1).ToString(CultureInfo.InvariantCulture);
                var numberWidth = measurer.Measure(number, style).Width;

                context.DrawText(number, content.X + gutter - GutterPadding - numberWidth, y, style, gutterColor);
            }

            context.Save();
            context.ClipRect(text);

            DrawSelection(context, measurer, style, line, text.X - scrollX, y);

            context.DrawText(buffer.GetLine(line), text.X - scrollX, y, style, color);

            if (focused && line == buffer.Line)
            {
                var caretX = text.X - scrollX + Width(measurer, style, buffer.GetLine(line)[..buffer.Column]);

                context.FillRect(new Rect(caretX, y, 1, lineHeight), GetValue(CaretColorProperty));
            }

            context.Restore();
        }
    }

    /// <summary>Fills the selected part of a line.</summary>
    /// <param name="context">Where to draw.</param>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="style">The font.</param>
    /// <param name="line">The line.</param>
    /// <param name="x">Where the line's text starts.</param>
    /// <param name="y">The top of the line.</param>
    void DrawSelection(IDrawingContext context, ITextMeasurer measurer, TextStyle style, int line, double x, double y)
    {
        if (!buffer.HasSelection)
            return;

        var start = buffer.LineStart(line);
        var end = buffer.LineEnd(line);
        var from = Math.Max(buffer.SelectionStart, start);
        var to = Math.Min(buffer.SelectionEnd, end + 1);

        if (from >= to)
            return;

        var text = buffer.GetLine(line);
        var layout = new TextLayout(measurer, text, style);
        var left = layout.WidthTo(from - start);
        var right = layout.WidthTo(Math.Min(to, end) - start);

        if (to > end)
            right += Width(measurer, style, " ");

        context.FillRect(new Rect(x + left, y, right - left, lineHeight), GetValue(SelectionColorProperty));
    }

    /// <summary>Measures the width of a piece of text.</summary>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="style">The font.</param>
    /// <param name="piece">The text.</param>
    /// <returns>The width in pixels.</returns>
    static double Width(ITextMeasurer measurer, TextStyle style, string piece)
        => piece.Length == 0 ? 0 : measurer.Measure(piece, style).Width;

    /// <summary>Gets the width of the column with the line numbers.</summary>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="style">The font.</param>
    /// <returns>The width, from the digits of the highest line number plus the space around them.</returns>
    double GutterWidth(ITextMeasurer measurer, TextStyle style)
    {
        if (!ShowLineNumbers)
            return 0;

        var digits = buffer.LineCount.ToString(CultureInfo.InvariantCulture).Length;

        return Width(measurer, style, new string('9', digits)) + (2 * GutterPadding);
    }

    /// <summary>Scrolls so that the caret is inside the text area.</summary>
    /// <param name="measurer">Measures the text.</param>
    /// <param name="style">The font.</param>
    /// <param name="text">The area of the text.</param>
    void Reveal(ITextMeasurer measurer, TextStyle style, Rect text)
    {
        revealCaret = false;

        var top = buffer.Line * lineHeight;

        scroll.EnsureVisible(top, lineHeight);

        var caretX = new TextLayout(measurer, buffer.GetLine(buffer.Line), style).WidthTo(buffer.Column);

        if (caretX < scrollX)
            scrollX = caretX;

        if (caretX + TextPadding > scrollX + text.Width)
            scrollX = caretX + TextPadding - text.Width;
    }

    /// <summary>Gets the offset in the text that is nearest to a position.</summary>
    /// <param name="x">The horizontal position relative to the border box.</param>
    /// <param name="y">The vertical position relative to the border box.</param>
    /// <returns>The offset.</returns>
    int OffsetAt(double x, double y)
    {
        var measurer = Measurer();
        var style = CurrentTextStyle();
        var content = ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height));
        var line = Math.Clamp((int)Math.Floor((y - content.Y + scroll.Offset) / lineHeight), 0, buffer.LineCount - 1);
        var target = x - content.X - GutterWidth(measurer, style) + scrollX;
        var text = buffer.GetLine(line);
        var best = new TextLayout(measurer, text, style).IndexAt(target);

        return buffer.LineStart(line) + best;
    }

    /// <summary>Moves the caret by a page of lines.</summary>
    /// <param name="direction">-1 for up, 1 for down.</param>
    /// <param name="extend">Whether to select while moving.</param>
    void MovePage(int direction, bool extend)
    {
        var lines = Math.Max(1, (int)(viewportHeight / lineHeight) - 1);

        Action<bool> move = direction < 0 ? buffer.MoveUp : buffer.MoveDown;

        for (var i = 0; i < lines; i++)
            move(extend);
    }

    /// <summary>Puts the selected text on the clipboard.</summary>
    void Copy()
    {
        if (buffer.HasSelection)
            Clipboard?.SetText(buffer.SelectedText);
    }

    /// <summary>Reacts to a change of the buffer by reporting a change of text.</summary>
    void OnBufferChanged()
    {
        revealCaret = true;

        if (buffer.Text == reported)
            return;

        reported = buffer.Text;

        TextChanged?.Invoke(this, EventArgs.Empty);
    }
}
