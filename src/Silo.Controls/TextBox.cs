using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A single line of text that the user can edit.
/// </summary>
/// <remarks>
/// <para>
/// A TextBox takes the keyboard focus when it is clicked, and then has the <c>focus</c> Pseudo-state. It handles typed text,
/// Backspace, Delete, the left and right arrows, Home and End, and raises <see cref="Submitted"/> on Enter. It scrolls
/// sideways to keep the caret visible. Selection, copy and paste, and undo arrive later.
/// </para>
/// <para>
/// The <c>caret-color</c> and <c>placeholder-color</c> properties set the caret and the <see cref="Placeholder"/>. Its width
/// comes from <c>width</c>, or is at least 120px.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var name = new TextBox { Placeholder = "Your name" };
///
/// name.Submitted += (_, _) => greeter.Greet(name.Text);
/// </code>
/// </example>
public class TextBox : Control
{
    /// <summary>The smallest content width in pixels when no <c>width</c> is set.</summary>
    const double MinimumWidth = 120;

    /// <summary>The color of the caret.</summary>
    public static readonly StyledProperty<Color> CaretColorProperty =
        StyledProperty.Register<TextBox, Color>("caret-color", new(0, 0, 0), Color.TryParse);

    /// <summary>The color of the placeholder text.</summary>
    public static readonly StyledProperty<Color> PlaceholderColorProperty =
        StyledProperty.Register<TextBox, Color>("placeholder-color", new(156, 163, 175), Color.TryParse);


    /// <summary>The text.</summary>
    string text = string.Empty;

    /// <summary>The position of the caret, from 0 to the length of the text.</summary>
    int caret;

    /// <summary>How far the text is scrolled to the left when it was last drawn, in pixels.</summary>
    double scroll;


    /// <summary>
    /// Runs before the first TextBox is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TextBox()
    {
    }

    /// <summary>Raised after <see cref="Text"/> changed.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Raised when the user presses Enter in the TextBox.</summary>
    public event EventHandler? Submitted;

    /// <summary>Gets or sets the text. Setting it moves the caret to the end.</summary>
    public string Text
    {
        get => text;
        set
        {
            var next = value ?? string.Empty;

            if (next == text)
                return;

            text = next;
            caret = next.Length;

            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the text that is shown, dimmed, while <see cref="Text"/> is empty.</summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>Gets the position of the caret: the number of characters before it.</summary>
    public int CaretIndex => caret;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Moves the caret to the character boundary nearest to a pointer position.</summary>
    /// <param name="x">The horizontal position relative to the TextBox's border box.</param>
    /// <returns><see langword="true"/> when the caret moved, so a new frame is needed.</returns>
    public bool PointerDown(double x)
    {
        var target = x - ContentBox(new Rect(0, 0, Bounds.Width, Bounds.Height)).X + scroll;
        var before = caret;
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

        caret = best;

        return before != caret;
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = caret;

        switch (key)
        {
            case Key.Left:
                caret = Math.Max(0, caret - 1);
                break;

            case Key.Right:
                caret = Math.Min(text.Length, caret + 1);
                break;

            case Key.Home:
                caret = 0;
                break;

            case Key.End:
                caret = text.Length;
                break;

            case Key.Backspace when caret > 0:
                return Replace(caret - 1, 1, string.Empty);

            case Key.Delete when caret < text.Length:
                return Replace(caret, 1, string.Empty);

            case Key.Enter:
                Submitted?.Invoke(this, EventArgs.Empty);
                return false;
        }

        return before != caret;
    }

    /// <inheritdoc/>
    public override bool TextInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var clean = string.Concat(input.Where(c => !char.IsControl(c)));

        if (clean.Length == 0)
            return false;

        return Replace(caret, 0, clean);
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var line = Measurer().Measure("Mg", CurrentTextStyle());

        return new Size(Math.Max(MinimumWidth, WidthOf(text)), line.Height);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var y = content.Y + Math.Max(0, content.Height - Measurer().Measure("Mg", style).Height) / 2;
        var caretX = WidthOf(text[..caret]);

        scroll = Math.Max(0, caretX - content.Width + 1);

        context.Save();
        context.ClipRect(content);

        if (text.Length == 0)
            context.DrawText(Placeholder, content.X, y, style, GetValue(PlaceholderColorProperty));
        else
            context.DrawText(text, content.X - scroll, y, style, GetValue(ColorProperty));

        if (PseudoStates.Contains("focus"))
        {
            var bar = new Rect(content.X + caretX - scroll, y, 1, style.Size * 1.2);

            context.FillRect(bar, GetValue(CaretColorProperty));
        }

        context.Restore();
    }

    /// <summary>Decides whether an edit by the user may produce a text.</summary>
    /// <param name="candidate">The text the edit would produce.</param>
    /// <returns><see langword="true"/> to accept the edit. The default accepts every text.</returns>
    /// <remarks>Override it to restrict what can be typed, as <see cref="NumberBox"/> does. Setting <see cref="Text"/> is not checked.</remarks>
    protected virtual bool Accepts(string candidate) => true;

    /// <summary>Replaces part of the text and puts the caret after the replacement.</summary>
    /// <param name="start">The index of the first character to replace.</param>
    /// <param name="length">The number of characters to replace.</param>
    /// <param name="replacement">The new text.</param>
    /// <returns><see langword="false"/> when <see cref="Accepts"/> refused the edit and nothing changed.</returns>
    bool Replace(int start, int length, string replacement)
    {
        var next = text[..start] + replacement + text[(start + length)..];

        if (!Accepts(next))
            return false;

        text = next;
        caret = start + replacement.Length;

        TextChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Measures the width of a piece of text.</summary>
    /// <param name="piece">The text.</param>
    /// <returns>The width in pixels.</returns>
    double WidthOf(string piece) => piece.Length == 0 ? 0 : Measurer().Measure(piece, CurrentTextStyle()).Width;
}
