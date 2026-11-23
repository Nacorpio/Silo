using Silo.Layout;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A panel for picking a color: a saturation/value square, a hue bar, an optional alpha bar and a hex field.
/// </summary>
/// <remarks>
/// <para>
/// The square sets the saturation and the value, the hue bar the hue, and the alpha bar, which <see cref="ShowAlpha"/> shows,
/// the opacity. The hex field shows the color as <c>#rrggbb</c>, or <c>#rrggbbaa</c> when it is not opaque. It accepts
/// <c>#rgb</c>, <c>#rrggbb</c> and <c>#rrggbbaa</c> with or without the <c>#</c>, applies the color as soon as the text is
/// one of these, and goes back to the current color when Enter is pressed on something else. <see cref="ColorChanged"/> is
/// raised on every step of a drag.
/// </para>
/// <para>
/// With the keyboard, Tab and Shift+Tab move between the parts and the arrows work as described on <see cref="ColorSurface"/>.
/// A panel in a window gets its keys from the focus as usual; a panel in an overlay, such as the one of a
/// <see cref="ColorPicker"/>, is given the keys with <see cref="KeyDown(Key, KeyModifiers)"/> and <see cref="TextInput"/>.
/// </para>
/// <para>
/// The look comes from the theme through the Classes <c>square</c>, <c>hue</c>, <c>alpha</c> and <c>hex</c> on the parts. Give
/// the surfaces a <c>height</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var panel = new ColorPanel { Color = new Color(59, 130, 246), ShowAlpha = true };
///
/// panel.ColorChanged += (_, _) => preview.Background = panel.Color;
/// </code>
/// </example>
public class ColorPanel : Control
{
    /// <summary>The saturation/value square.</summary>
    readonly ColorSurface square = new(ColorSurfaceKind.SaturationValue);

    /// <summary>The hue bar.</summary>
    readonly ColorSurface hueBar = new(ColorSurfaceKind.Hue);

    /// <summary>The alpha bar.</summary>
    readonly ColorSurface alphaBar = new(ColorSurfaceKind.Alpha);

    /// <summary>The field that shows the color as text.</summary>
    readonly TextBox hex = new();


    /// <summary>The current color.</summary>
    Color color = new(59, 130, 246);

    /// <summary>The hue in degrees, kept apart from the color since a gray has none.</summary>
    double hue;

    /// <summary>The saturation, from 0 to 1.</summary>
    double saturation;

    /// <summary>The value (brightness), from 0 to 1.</summary>
    double brightness;

    /// <summary>Whether the alpha bar is shown.</summary>
    bool showAlpha;

    /// <summary>The part that the keys go to while the panel is driven from outside, or <see langword="null"/> when none is.</summary>
    Control? active;

    /// <summary>Whether the hex field is being written from the color, so that its changes must not be read back.</summary>
    bool syncing;


    /// <summary>Creates a ColorPanel with the color set to the default accent blue.</summary>
    public ColorPanel()
    {
        square.AddClass("square");
        hueBar.AddClass("hue");
        alphaBar.AddClass("alpha");
        hex.AddClass("hex");

        square.Changed += (_, _) => OnSquareChanged();
        hueBar.Changed += (_, _) => OnHueChanged();
        alphaBar.Changed += (_, _) => OnAlphaChanged();
        square.Pressed += (_, _) => Activate(square);
        hueBar.Pressed += (_, _) => Activate(hueBar);
        alphaBar.Pressed += (_, _) => Activate(alphaBar);
        hex.Pressed += (_, _) => Activate(hex);
        hex.TextChanged += (_, _) => OnHexChanged();
        hex.Submitted += (_, _) => OnSubmitted();

        Children.Add(square);
        Children.Add(hueBar);
        Children.Add(hex);

        (hue, saturation, brightness) = ToHsv(color);

        Refresh();
    }

    /// <summary>Raised after <see cref="Color"/> changed, also while the user is still dragging.</summary>
    public event EventHandler? ColorChanged;

    /// <summary>Raised when the user presses Enter, which confirms the color. A ColorPicker closes its panel then.</summary>
    public event EventHandler? Submitted;

    /// <summary>Gets or sets the color.</summary>
    public Color Color
    {
        get => color;
        set => Apply(value, fromHex: false);
    }

    /// <summary>Gets or sets whether the alpha bar is shown. The default is <see langword="false"/>.</summary>
    /// <remarks>The alpha of <see cref="Color"/> is kept either way. It only cannot be changed with the pointer while the bar is hidden.</remarks>
    public bool ShowAlpha
    {
        get => showAlpha;
        set
        {
            if (value == showAlpha)
                return;

            showAlpha = value;

            if (value)
                Children.Insert(2, alphaBar);
            else
                Children.Remove(alphaBar);
        }
    }

    /// <summary>Gives the first part the focus look, for a panel that is driven with forwarded keys.</summary>
    /// <remarks>A <see cref="ColorPicker"/> calls this when it opens its panel in an overlay, where nothing has the keyboard focus.</remarks>
    public void BeginKeyboard() => Activate(square);

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (key == Key.Tab)
        {
            var parts = Parts();
            var index = Math.Max(0, parts.IndexOf(active!));
            var next = modifiers.HasFlag(KeyModifiers.Shift) ? index - 1 + parts.Count : index + 1;

            Activate(parts[next % parts.Count]);

            return true;
        }

        if (active is null)
            return false;

        var handled = active.KeyDown(key, modifiers);

        if (key == Key.Enter && active != hex)
        {
            Submitted?.Invoke(this, EventArgs.Empty);

            return true;
        }

        return handled;
    }

    /// <inheritdoc/>
    public override bool TextInput(string text) => active == hex && hex.TextInput(text);

    /// <summary>Gets the parts that Tab goes through, in order.</summary>
    /// <returns>The visible surfaces and the hex field.</returns>
    List<Control> Parts() => [square, hueBar, .. showAlpha ? new[] { alphaBar } : [], hex];

    /// <summary>Gives the focus look to one part and takes it from the previous one.</summary>
    /// <param name="part">The part that the keys go to from now on.</param>
    void Activate(Control part)
    {
        if (active == part)
            return;

        active?.SetFocused(false);

        active = part;

        part.SetFocused(true);
    }

    /// <summary>Takes the position of the square as the saturation and the value.</summary>
    void OnSquareChanged()
    {
        saturation = square.X;
        brightness = 1 - square.Y;

        Activate(square);
        Apply(FromHsv(hue, saturation, brightness, color.A), fromHex: false, fromSurface: true);
    }

    /// <summary>Takes the position of the hue bar as the hue.</summary>
    void OnHueChanged()
    {
        hue = hueBar.X * 360;

        Activate(hueBar);
        Apply(FromHsv(hue, saturation, brightness, color.A), fromHex: false, fromSurface: true);
    }

    /// <summary>Takes the position of the alpha bar as the opacity.</summary>
    void OnAlphaChanged()
    {
        Activate(alphaBar);
        Apply(color with { A = (byte)Math.Round(alphaBar.X * 255) }, fromHex: false, fromSurface: true);
    }

    /// <summary>Reads the hex field, and applies it when it is a whole color.</summary>
    void OnHexChanged()
    {
        if (!syncing && ParseHex(hex.Text) is { } parsed)
            Apply(parsed, fromHex: true);
    }

    /// <summary>Writes the color back into the hex field, which drops what was typed that is not a color, and reports Enter.</summary>
    void OnSubmitted()
    {
        Refresh();

        Submitted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets the color and shows it in the parts.</summary>
    /// <param name="next">The new color.</param>
    /// <param name="fromHex">Whether the user typed it, in which case the hex field keeps the text as it is.</param>
    /// <param name="fromSurface">Whether a surface produced it, so the hue is kept as the surface set it.</param>
    void Apply(Color next, bool fromHex, bool fromSurface = false)
    {
        if (next == color)
            return;

        color = next;

        if (!fromSurface)
        {
            var (h, s, v) = ToHsv(next);

            // A gray has no hue and black has no saturation, so the panel keeps what it had instead of jumping to red.
            hue = s > 0 && v > 0 ? h : hue;
            saturation = s;
            brightness = v;
        }

        Refresh(writeHex: !fromHex);

        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves the thumbs and the hex text to the color.</summary>
    /// <param name="writeHex">Whether to rewrite the hex field.</param>
    void Refresh(bool writeHex = true)
    {
        square.Hue = hue;
        square.X = saturation;
        square.Y = 1 - brightness;
        hueBar.X = hue / 360;
        alphaBar.BaseColor = color;
        alphaBar.X = color.A / 255.0;

        if (!writeHex)
            return;

        syncing = true;

        hex.Text = color.ToString();

        syncing = false;
    }

    /// <summary>Reads text as a hex color.</summary>
    /// <param name="text">The text, with or without the <c>#</c>.</param>
    /// <returns>The color, or <see langword="null"/> when the text is not <c>rgb</c>, <c>rrggbb</c> or <c>rrggbbaa</c> in hex digits.</returns>
    static Color? ParseHex(string text)
    {
        var digits = text.Trim().TrimStart('#');

        if (digits.Length is not (3 or 6 or 8) || !digits.All(Uri.IsHexDigit))
            return null;

        return Color.TryParse($"#{digits}", out var parsed) ? parsed : null;
    }

    /// <summary>Converts a color to hue, saturation and value.</summary>
    /// <param name="color">The color. Its alpha is ignored.</param>
    /// <returns>The hue in degrees, from 0 to 360, and the saturation and the value, from 0 to 1.</returns>
    public static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var (r, g, b) = (color.R / 255.0, color.G / 255.0, color.B / 255.0);
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));

        var hue = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);

        return ((hue + 360) % 360, max == 0 ? 0 : delta / max, max);
    }

    /// <summary>Converts hue, saturation and value to a color.</summary>
    /// <param name="hue">The hue in degrees.</param>
    /// <param name="saturation">The saturation, from 0 to 1.</param>
    /// <param name="value">The value, from 0 to 1.</param>
    /// <param name="alpha">The alpha of the color.</param>
    /// <returns>The color.</returns>
    public static Color FromHsv(double hue, double saturation, double value, byte alpha = 255)
    {
        var chroma = value * saturation;
        var sector = ((hue % 360) + 360) % 360 / 60;
        var second = chroma * (1 - Math.Abs((sector % 2) - 1));
        var offset = value - chroma;

        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };

        return new Color(ToByte(r + offset), ToByte(g + offset), ToByte(b + offset), alpha);
    }

    /// <summary>Converts a fraction to a byte.</summary>
    /// <param name="fraction">The fraction, from 0 to 1.</param>
    /// <returns>The byte, from 0 to 255.</returns>
    static byte ToByte(double fraction) => (byte)Math.Round(Math.Clamp(fraction, 0, 1) * 255);
}
