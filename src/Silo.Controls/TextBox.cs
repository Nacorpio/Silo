using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A single line of text that the user can edit.
/// </summary>
/// <remarks>
/// <para>
/// A TextBox takes the keyboard focus when it is clicked, and then has the <c>focus</c> Pseudo-state. It shows a
/// single-line <see cref="TextBuffer"/>, which does the editing: typed text, Backspace, Delete, the arrows, Home and End, and
/// with Shift a selection, which a drag selects too. Ctrl+A selects all, Ctrl+Z undoes, Ctrl+Y or Ctrl+Shift+Z redoes, and
/// Ctrl+C, Ctrl+X and Ctrl+V copy, cut and paste when <see cref="Clipboard"/> is set. Enter raises <see cref="Submitted"/>.
/// It scrolls sideways to keep the caret visible.
/// </para>
/// <para>
/// The <c>caret-color</c>, <c>selection-color</c> and <c>placeholder-color</c> properties set the caret, the selection and the
/// <see cref="Placeholder"/>. Its width comes from <c>width</c>, or is at least 120px.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var name = new TextBox { Placeholder = "Your name" };
///
/// name.Submitted += (_, _) => greeter.Greet(name.Text);
/// </code>
/// </example>
[PseudoStates("hover", "focus")]
public class TextBox : Control, IPointerHandler
{
    /// <summary>The smallest content width in pixels when no <c>width</c> is set.</summary>
    const double MinimumWidth = 120;

    /// <summary>The color of the caret.</summary>
    public static readonly StyledProperty<Color> CaretColorProperty =
        StyledProperty.Register<TextBox, Color>("caret-color", new(0, 0, 0), Color.TryParse);

    /// <summary>The color of the selected text's background.</summary>
    public static readonly StyledProperty<Color> SelectionColorProperty =
        StyledProperty.Register<TextBox, Color>("selection-color", new(59, 130, 246, 90), Color.TryParse);

    /// <summary>The color of the placeholder text.</summary>
    public static readonly StyledProperty<Color> PlaceholderColorProperty =
        StyledProperty.Register<TextBox, Color>("placeholder-color", new(156, 163, 175), Color.TryParse);


    /// <summary>The buffer that holds the text and does the editing.</summary>
    readonly TextBuffer buffer = new(singleLine: true);

    /// <summary>The text that <see cref="TextChanged"/> last reported.</summary>
    string reported = string.Empty;

    /// <summary>Whether a press started a selection that the pointer is still dragging.</summary>
    bool selecting;

    /// <summary>How far the text is scrolled to the left when it was last drawn, in pixels.</summary>
    double scroll;


    /// <summary>
    /// Runs before the first TextBox is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TextBox()
    {
    }

    /// <summary>Creates an empty TextBox.</summary>
    public TextBox()
    {
        buffer.Accepts = candidate => Accepts(candidate);
        buffer.Changed += (_, _) => OnBufferChanged();
    }

    /// <summary>Raised after <see cref="Text"/> changed.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Raised when the pointer goes down on the TextBox, before the caret moves.</summary>
    public event EventHandler? Pressed;

    /// <summary>Raised when the user presses Enter in the TextBox.</summary>
    public event EventHandler? Submitted;

    /// <summary>Gets or sets the text. Setting it moves the caret to the end and forgets what could be undone.</summary>
    public string Text
    {
        get => buffer.Text;
        set
        {
            var next = value ?? string.Empty;

            if (next == buffer.Text)
                return;

            buffer.Reset(next);
            buffer.MoveToEnd(extend: false);
        }
    }

    /// <summary>Gets the single-line buffer that holds the text, for its selection, caret and undo.</summary>
    public TextBuffer Buffer => buffer;

    /// <summary>Gets or sets the clipboard that copy, cut and paste use. Without one they do nothing.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>Gets or sets the text that is shown, dimmed, while <see cref="Text"/> is empty.</summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>Gets the position of the caret: the number of characters before it.</summary>
    public int CaretIndex => buffer.Caret;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Moves the caret to the character boundary nearest to a pointer position and starts a selection there.</summary>
    /// <param name="e">The pointer position relative to the TextBox's border box.</param>
    /// <returns><see langword="true"/> when the caret or the selection changed, so a new frame is needed.</returns>
    public virtual bool PointerDown(PointerEvent e)
    {
        Pressed?.Invoke(this, EventArgs.Empty);

        selecting = true;

        return Move(OffsetAt(e.X), extend: false);
    }

    /// <summary>Selects from where the press started up to the pointer.</summary>
    /// <param name="e">The pointer position relative to the TextBox's border box.</param>
    /// <returns><see langword="true"/> when the selection changed.</returns>
    public bool PointerDrag(PointerEvent e) => selecting && Move(OffsetAt(e.X), extend: true);

    /// <summary>Ends the selection that a drag made.</summary>
    /// <param name="e">The pointer position relative to the TextBox's border box.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool PointerUp(PointerEvent e)
    {
        selecting = false;

        return false;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => Command(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
        => modifiers == KeyModifiers.None ? KeyDown(key) : Command(key, modifiers);

    /// <inheritdoc/>
    public override bool TextInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var clean = string.Concat(input.Where(c => !char.IsControl(c)));

        if (clean.Length == 0)
            return false;

        return Edit(() => buffer.Insert(clean));
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var line = Measurer().Measure("Mg", CurrentTextStyle());

        return new Size(Math.Max(MinimumWidth, WidthOf(buffer.Text)), line.Height);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var y = content.Y + Math.Max(0, content.Height - Measurer().Measure("Mg", style).Height) / 2;
        var text = buffer.Text;
        var caretX = WidthOf(text[..buffer.Caret]);

        scroll = Math.Max(0, caretX - content.Width + 1);

        context.Save();
        context.ClipRect(content);

        if (buffer.HasSelection)
        {
            var from = WidthOf(text[..buffer.SelectionStart]);
            var to = WidthOf(text[..buffer.SelectionEnd]);

            context.FillRect(new Rect(content.X + from - scroll, y, to - from, style.Size * 1.2), GetValue(SelectionColorProperty));
        }

        if (text.Length == 0)
            context.DrawText(Placeholder, content.X, y, style, GetValue(PlaceholderColorProperty));
        else
            context.DrawText(Display(text), content.X - scroll, y, style, GetValue(ColorProperty));

        if (PseudoStates.Contains("focus"))
        {
            var bar = new Rect(content.X + caretX - scroll, y, 1, style.Size * 1.2);

            context.FillRect(bar, GetValue(CaretColorProperty));
        }

        context.Restore();
    }

    /// <summary>Gets a value indicating whether copy and cut may put the selected text on the clipboard. The default is <see langword="true"/>.</summary>
    /// <remarks>Override it for a TextBox that hides its text, as <see cref="PasswordBox"/> does.</remarks>
    protected virtual bool CanCopy => true;

    /// <summary>Turns text into what is drawn for it.</summary>
    /// <param name="value">The text, or the start of it.</param>
    /// <returns>The text to draw. The default is the text itself.</returns>
    /// <remarks>
    /// Override it to draw something else, as <see cref="PasswordBox"/> does. It must map each character on its own, so that the
    /// caret and the pointer land between the right characters.
    /// </remarks>
    protected virtual string Display(string value) => value;

    /// <summary>Decides whether an edit by the user may produce a text.</summary>
    /// <param name="candidate">The text the edit would produce.</param>
    /// <returns><see langword="true"/> to accept the edit. The default accepts every text.</returns>
    /// <remarks>Override it to restrict what can be typed, as <see cref="NumberBox"/> does. Setting <see cref="Text"/> is not checked.</remarks>
    protected virtual bool Accepts(string candidate) => true;

    /// <summary>Runs a key command for the keys and modifiers that a TextBox handles.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys.</param>
    /// <returns><see langword="true"/> when the TextBox changed, so a new frame is needed.</returns>
    bool Command(Key key, KeyModifiers modifiers)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var control = modifiers.HasFlag(KeyModifiers.Control);

        switch (key)
        {
            case Key.Left:
                return Edit(() => buffer.MoveLeft(shift));

            case Key.Right:
                return Edit(() => buffer.MoveRight(shift));

            case Key.Home:
                return Edit(() => buffer.MoveTo(0, shift));

            case Key.End:
                return Edit(() => buffer.MoveToEnd(shift));

            case Key.Backspace:
                return Edit(buffer.Backspace);

            case Key.Delete:
                return Edit(buffer.Delete);

            case Key.Enter:
                Submitted?.Invoke(this, EventArgs.Empty);

                return false;

            case Key.A when control:
                return Edit(buffer.SelectAll);

            case Key.Z when control && shift:
            case Key.Y when control:
                return Edit(() => buffer.Redo());

            case Key.Z when control:
                return Edit(() => buffer.Undo());

            case Key.C when control:
                Copy();

                return false;

            case Key.X when control && CanCopy:
                Copy();

                return Edit(buffer.Delete);

            case Key.V when control:
                return Clipboard?.GetText() is { Length: > 0 } pasted && Edit(() => buffer.Insert(pasted));
        }

        return false;
    }

    /// <summary>Runs a change of the buffer and tells whether it changed anything the TextBox shows.</summary>
    /// <param name="change">The change.</param>
    /// <returns><see langword="true"/> when the text, the caret or the selection differ afterwards.</returns>
    bool Edit(Action change)
    {
        var (text, caret, anchor) = (buffer.Text, buffer.Caret, buffer.Anchor);

        change();

        return text != buffer.Text || caret != buffer.Caret || anchor != buffer.Anchor;
    }

    /// <summary>Moves the caret and tells whether the caret or the selection changed.</summary>
    /// <param name="offset">The new offset.</param>
    /// <param name="extend">Whether to keep the anchor.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    bool Move(int offset, bool extend) => Edit(() => buffer.MoveTo(offset, extend));

    /// <summary>Puts the selected text on the clipboard, unless the TextBox hides its text.</summary>
    void Copy()
    {
        if (buffer.HasSelection && CanCopy)
            Clipboard?.SetText(buffer.SelectedText);
    }

    /// <summary>Finds the character boundary nearest to a pointer position.</summary>
    /// <param name="x">The position relative to the TextBox's border box.</param>
    /// <returns>The offset into the text.</returns>
    int OffsetAt(double x)
    {
        var target = x - ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height)).X + scroll;
        var text = buffer.Text;
        var best = 0;
        var bestDistance = double.MaxValue;

        for (var i = 0; i <= text.Length; i++)
        {
            var distance = Math.Abs(WidthOf(text[..i]) - target);

            if (distance >= bestDistance)
                continue;

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Raises <see cref="TextChanged"/> when the buffer holds another text than the one last reported.</summary>
    void OnBufferChanged()
    {
        if (buffer.Text == reported)
            return;

        reported = buffer.Text;

        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Measures the width of a piece of text.</summary>
    /// <param name="piece">The text.</param>
    /// <returns>The width in pixels.</returns>
    double WidthOf(string piece) =>
        piece.Length == 0 ? 0 : Measurer().Measure(Display(piece), CurrentTextStyle()).Width;
}
