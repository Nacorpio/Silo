using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A field that shows an emoji and drops down an <see cref="EmojiPanel"/> to pick another.
/// </summary>
/// <remarks>
/// <para>
/// Pressing the EmojiPicker opens the panel in the window's <see cref="OverlayLayer"/>, right below it. Picking an emoji sets
/// <see cref="SelectedEmoji"/> and closes the panel, unless <see cref="CloseOnPick"/> is off; a press anywhere else or Escape closes it
/// unchanged. With the keyboard focus, Enter opens it and then typing searches. While there is no emoji, the <see cref="Placeholder"/>
/// is shown in the <c>placeholder-color</c>.
/// </para>
/// <para>
/// <see cref="Recent"/> and <see cref="SkinTone"/> live on the picker, so that they survive the panel being closed and opened again.
/// The picker never saves them: listen to <see cref="RecentChanged"/> and <see cref="SkinToneChanged"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var reaction = new EmojiPicker { SelectedEmoji = "\U0001F44D" };
///
/// reaction.EmojiPicked += (_, emoji) => message.React(emoji);
/// </code>
/// </example>
[PseudoStates("hover", "focus")]
public class EmojiPicker : Control, IPointerHandler
{
    /// <summary>The color of the placeholder text.</summary>
    public static readonly StyledProperty<Color> PlaceholderColorProperty =
        StyledProperty.Register<EmojiPicker, Color>("placeholder-color", new(156, 163, 175), Color.TryParse);

    /// <summary>The font that the emoji is drawn in.</summary>
    public static readonly StyledProperty<string> EmojiFontFamilyProperty =
        StyledProperty.Register<EmojiPicker, string>("emoji-font-family", EmojiFonts.Default, ValueParsers.Text);


    /// <summary>Opens the panel below the picker and tracks whether it is open.</summary>
    readonly AnchoredPopup popup;

    /// <summary>The recent emoji, newest first.</summary>
    List<string> recent = [];

    /// <summary>The panel while it is open.</summary>
    EmojiPanel? panel;

    /// <summary>The picked emoji.</summary>
    string? selected;

    /// <summary>The skin tone.</summary>
    SkinTone skinTone;

    /// <summary>Whether a pick closes the panel.</summary>
    bool closeOnPick = true;


    static EmojiPicker()
    {
    }

    /// <summary>Creates a picker with no emoji picked.</summary>
    public EmojiPicker() => popup = new AnchoredPopup(this);


    /// <summary>Occurs when an emoji is picked, even when it is the one that was picked already.</summary>
    public event EventHandler<string>? EmojiPicked;

    /// <summary>Occurs when <see cref="SelectedEmoji"/> changed.</summary>
    public event EventHandler? SelectedEmojiChanged;

    /// <summary>Occurs when <see cref="Recent"/> changed because of a pick.</summary>
    public event EventHandler? RecentChanged;

    /// <summary>Occurs when the skin tone was stepped in the open panel.</summary>
    public event EventHandler? SkinToneChanged;


    /// <summary>Gets or sets the picked emoji, or <see langword="null"/> for none.</summary>
    public string? SelectedEmoji
    {
        get => selected;
        set
        {
            if (value == selected)
                return;

            selected = value;

            SelectedEmojiChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the text shown while no emoji is picked.</summary>
    public string Placeholder { get; set; } = "Emoji";

    /// <summary>Gets or sets the skin tone of the emoji that have tones.</summary>
    public SkinTone SkinTone
    {
        get => skinTone;
        set
        {
            skinTone = value;

            if (panel is not null)
                panel.SkinTone = value;
        }
    }

    /// <summary>Gets or sets the recent emoji, newest first.</summary>
    public IReadOnlyList<string> Recent
    {
        get => recent;
        set
        {
            recent = [.. (value ?? []).Distinct().Take(EmojiPanel.MaxRecent)];

            if (panel is not null)
                panel.Recent = recent;
        }
    }

    /// <summary>Gets or sets whether a pick closes the panel. The default is <see langword="true"/>.</summary>
    public bool CloseOnPick
    {
        get => closeOnPick;
        set
        {
            closeOnPick = value;

            if (panel is not null)
                panel.CloseOnPick = value;
        }
    }

    /// <summary>Gets whether the panel is open.</summary>
    public bool IsOpen => popup.IsOpen;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Opens the panel below the picker.</summary>
    /// <returns><see langword="true"/> when it opened. It does not when it is open already or the picker is not in a window.</returns>
    public bool Open()
    {
        if (IsOpen)
            return false;

        var view = new EmojiPanel { SkinTone = skinTone, Recent = recent, CloseOnPick = closeOnPick };

        view.AddClass("popup");

        view.EmojiPicked += (_, emoji) =>
        {
            SelectedEmoji = emoji;

            EmojiPicked?.Invoke(this, emoji);
        };
        view.RecentChanged += (_, _) =>
        {
            recent = [.. view.Recent];

            RecentChanged?.Invoke(this, EventArgs.Empty);
        };
        view.SkinToneChanged += (_, _) =>
        {
            skinTone = view.SkinTone;

            SkinToneChanged?.Invoke(this, EventArgs.Empty);
        };
        view.Submitted += (_, _) => Close();

        if (!popup.Open(view))
            return false;

        panel = view;

        view.BeginKeyboard();

        return true;
    }

    /// <summary>Closes the panel.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        panel = null;

        return popup.Close();
    }

    /// <inheritdoc/>
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
        var measurer = Measurer();
        var text = CurrentTextStyle();
        var glyph = GlyphStyle(text);
        var width = Math.Max(measurer.Measure(Placeholder, text).Width, measurer.Measure("\U0001F600", glyph).Width);

        return new Size(width + DropDownArrow.Space, Math.Max(measurer.LineHeight(text), measurer.LineHeight(glyph)));
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var measurer = Measurer();
        var style = selected is null ? CurrentTextStyle() : GlyphStyle(CurrentTextStyle());
        var text = selected ?? Placeholder;
        var y = content.Y + Math.Max(0, content.Height - measurer.LineHeight(style)) / 2;

        context.Save();
        context.ClipRect(new Rect(content.X, content.Y, Math.Max(0, content.Width - DropDownArrow.Space), content.Height));
        context.DrawText(text, content.X, y, style, GetValue(selected is null ? PlaceholderColorProperty : ColorProperty));
        context.Restore();

        DropDownArrow.Draw(context, content, GetValue(ColorProperty));
    }

    /// <summary>Gets the style that emoji are drawn in.</summary>
    /// <param name="text">The style of the text around them.</param>
    /// <returns>The emoji font at the size of the text.</returns>
    TextStyle GlyphStyle(TextStyle text) => text with { FontFamily = GetValue(EmojiFontFamilyProperty) };
}
