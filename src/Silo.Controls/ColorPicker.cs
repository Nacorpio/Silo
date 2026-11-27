using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A field that shows a color as a swatch and its hex code, and drops down a <see cref="ColorPanel"/> to pick another.
/// </summary>
/// <remarks>
/// <para>
/// Pressing the ColorPicker opens a ColorPanel in the window's <see cref="OverlayLayer"/>, right below it. <see cref="Color"/>
/// follows the panel while the user drags, so a preview can update live. The panel stays open until a press outside it,
/// Escape, or Enter on one of its surfaces. With the keyboard focus, Enter opens it, and then the keys go to the panel: Tab
/// moves between its parts, the arrows move the thumbs and typed text goes to the hex field.
/// </para>
/// <para><see cref="ShowAlpha"/> is passed on to the panel. The hex code is shown as <c>#rrggbb</c>, or <c>#rrggbbaa</c> when the color is not opaque.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var accent = new ColorPicker { Color = new Color(59, 130, 246) };
///
/// accent.ColorChanged += (_, _) => theme.Accent = accent.Color;
/// </code>
/// </example>
[PseudoStates("hover", "focus")]
public class ColorPicker : Control, IPointerHandler
{
    /// <summary>The side of the swatch in pixels.</summary>
    const double SwatchSize = 16;

    /// <summary>The space between the swatch and the text in pixels.</summary>
    const double SwatchGap = 8;


    /// <summary>Opens the panel below the ColorPicker and tracks whether it is open.</summary>
    readonly AnchoredPopup popup;


    /// <summary>The panel while it is open.</summary>
    ColorPanel? panel;

    /// <summary>The picked color.</summary>
    Color color = new(59, 130, 246);

    /// <summary>Whether the panel shows the alpha bar.</summary>
    bool showAlpha;


    /// <summary>
    /// Runs before the first ColorPicker is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ColorPicker()
    {
    }

    /// <summary>Creates a ColorPicker with a blue color.</summary>
    public ColorPicker() => popup = new AnchoredPopup(this);

    /// <summary>Raised after <see cref="Color"/> changed, also while the user is dragging in the panel.</summary>
    public event EventHandler? ColorChanged;

    /// <summary>Gets or sets the color.</summary>
    public Color Color
    {
        get => color;
        set
        {
            if (value == color)
                return;

            color = value;

            if (panel is not null)
                panel.Color = value;

            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets whether the panel has an alpha bar. The default is <see langword="false"/>.</summary>
    public bool ShowAlpha
    {
        get => showAlpha;
        set
        {
            showAlpha = value;

            if (panel is not null)
                panel.ShowAlpha = value;
        }
    }

    /// <summary>Gets whether the panel is open.</summary>
    public bool IsOpen => popup.IsOpen;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the hex code of the color.</summary>
    public string Text => color.ToString();

    /// <summary>Opens the panel below the ColorPicker.</summary>
    /// <returns><see langword="true"/> when it opened. It does not when it is open already or the ColorPicker is not in a window.</returns>
    /// <remarks>Call it after <see cref="Control.Arrange"/>, since the panel sits where the ColorPicker was arranged.</remarks>
    public bool Open()
    {
        if (IsOpen)
            return false;

        var view = new ColorPanel { Color = color, ShowAlpha = showAlpha };

        view.AddClass("popup");

        view.ColorChanged += (_, _) => Color = view.Color;
        view.Submitted += (_, _) => Close();

        if (!popup.Open(view))
            return false;

        panel = view;

        view.BeginKeyboard();

        return true;
    }

    /// <summary>Closes the panel. The color keeps what it was set to.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        panel = null;

        return popup.Close();
    }

    /// <summary>Opens the panel when it is closed. The pointer router calls this when the pointer goes down on the ColorPicker.</summary>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    /// <remarks>A press while the panel is open never gets here: the overlay layer closes the panel first.</remarks>
    public bool PointerDown(PointerEvent e) => Open();

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (IsOpen)
            return popup.KeyDown(key, modifiers);

        return key == Key.Enter && Open();
    }

    /// <inheritdoc/>
    public override bool TextInput(string text) => popup.TextInput(text);

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var width = measurer.Measure("#rrggbbaa", style).Width;

        return new Size(SwatchSize + SwatchGap + width + DropDownArrow.Space, Math.Max(SwatchSize, measurer.LineHeight(style)));
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var textY = content.Y + Math.Max(0, content.Height - Measurer().LineHeight(style)) / 2;
        var swatch = new Rect(content.X, content.Y + ((content.Height - SwatchSize) / 2), SwatchSize, SwatchSize);
        var textX = swatch.Right + SwatchGap;

        context.FillRect(swatch, new(255, 255, 255), 3);
        context.FillRect(swatch, color, 3);
        context.StrokeRect(swatch, new(0, 0, 0, 60), 1, 3);

        context.Save();
        context.ClipRect(new Rect(textX, content.Y, Math.Max(0, content.Right - DropDownArrow.Space - textX), content.Height));
        context.DrawText(Text, textX, textY, style, GetValue(ColorProperty));
        context.Restore();

        DropDownArrow.Draw(context, content, GetValue(ColorProperty));
    }
}
