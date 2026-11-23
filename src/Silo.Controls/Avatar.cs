using System.Globalization;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A picture of a person or an account, or their initials when there is no picture.
/// </summary>
/// <remarks>
/// <para>
/// With a <see cref="Source"/> the picture fills the Avatar, cropped to its shape. Without one, the Avatar shows up to two
/// <see cref="Initials"/> of the <see cref="DisplayName"/> in the <c>color</c> of the Avatar, on a background that is picked from the
/// name, so the same name always gets the same color. A <c>background</c> in the theme replaces that color.
/// </para>
/// <para>
/// The shape is the <c>border-radius</c>: the default theme makes a circle, a smaller radius makes a rounded square and 0 a
/// square. Give the Avatar a <c>width</c> and a <c>height</c>, since it has no content to size itself from.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var avatar = new Avatar { DisplayName = "Ada Lovelace" };
///
/// // shows "AL"
/// </code>
/// </example>
public class Avatar : Control
{
    /// <summary>The background colors for initials, which the name picks from.</summary>
    static readonly Color[] Palette =
    [
        new(59, 130, 246), new(16, 185, 129), new(245, 158, 11), new(239, 68, 68),
        new(139, 92, 246), new(236, 72, 153), new(20, 184, 166), new(100, 116, 139),
    ];


    /// <summary>The name.</summary>
    string name = string.Empty;


    /// <summary>
    /// Runs before the first Avatar is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Avatar()
    {
    }

    /// <summary>Gets or sets the picture, or <see langword="null"/> to show the initials.</summary>
    public IImage? Source { get; set; }

    /// <summary>Gets or sets the name of the person, which gives the initials and the background color.</summary>
    public string DisplayName
    {
        get => name;
        set => name = value ?? string.Empty;
    }

    /// <summary>Gets the initials of <see cref="DisplayName"/>: the first letters of its first two words, or the first two letters of a single word, in upper case.</summary>
    public string Initials => InitialsOf(name);

    /// <summary>Gets the initials of a name.</summary>
    /// <param name="value">The name.</param>
    /// <returns>Up to two letters in upper case, or an empty text for an empty name.</returns>
    public static string InitialsOf(string value)
    {
        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return string.Empty;

        var letters = words.Length == 1
            ? FirstElements(words[0], 2)
            : FirstElements(words[0], 1) + FirstElements(words[1], 1);

        return letters.ToUpperInvariant();
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        var radius = GetValue(BorderRadiusProperty);
        var themed = GetValue(BackgroundProperty);

        context.Save();
        context.ClipRoundRect(bounds, radius);

        if (Source is { } picture)
            DrawCover(context, picture, bounds);
        else
            DrawInitials(context, bounds, themed.A > 0 ? themed : ColorOf(name));

        context.Restore();

        DrawBorder(context, bounds);
    }

    /// <summary>Gets the background color for a name.</summary>
    /// <param name="value">The name.</param>
    /// <returns>One of the palette colors, always the same for the same name.</returns>
    static Color ColorOf(string value)
    {
        var sum = 0;

        foreach (var c in value)
            sum = unchecked((sum * 31) + char.ToLowerInvariant(c));

        return Palette[(sum & int.MaxValue) % Palette.Length];
    }

    /// <summary>Takes the first text elements, so that a letter with accents or an emoji is not cut in two.</summary>
    /// <param name="value">The text.</param>
    /// <param name="count">How many to take.</param>
    /// <returns>The elements.</returns>
    static string FirstElements(string value, int count)
    {
        var elements = StringInfo.GetTextElementEnumerator(value);
        var taken = string.Empty;

        while (taken.Length >= 0 && count-- > 0 && elements.MoveNext())
            taken += elements.GetTextElement();

        return taken;
    }

    /// <summary>Draws a picture so that it covers the whole Avatar, cropped at the edges.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="bounds">The area of the Avatar.</param>
    void DrawCover(IDrawingContext context, IImage picture, Rect bounds)
    {
        if (picture.Width <= 0 || picture.Height <= 0)
            return;

        var scale = Math.Max(bounds.Width / picture.Width, bounds.Height / picture.Height);
        var width = picture.Width * scale;
        var height = picture.Height * scale;
        var destination = new Rect(bounds.X + ((bounds.Width - width) / 2), bounds.Y + ((bounds.Height - height) / 2), width, height);

        Image.Draw(context, picture, destination, GetValue(ColorProperty));
    }

    /// <summary>Draws the background and the initials.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="bounds">The area of the Avatar.</param>
    /// <param name="background">The background color.</param>
    void DrawInitials(IDrawingContext context, Rect bounds, Color background)
    {
        context.FillRect(bounds, background);

        var text = Initials;

        if (text.Length == 0)
            return;

        var style = CurrentTextStyle();
        var size = Measurer().Measure(text, style);

        context.DrawText(text, bounds.X + ((bounds.Width - size.Width) / 2), bounds.Y + ((bounds.Height - size.Height) / 2), style, GetValue(ColorProperty));
    }
}
