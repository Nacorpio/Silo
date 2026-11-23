using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// The small box that appears when the pointer rests on a Control that has a <see cref="Control.Tooltip"/>: a line of text
/// and, when the Control has a <see cref="Control.TooltipShortcut"/>, the shortcut after it. It can also have an icon, a header
/// and a description, or hold a Control of your own.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TooltipPresenter"/> creates one when the pointer has rested long enough, so a Tooltip is rarely created by
/// hand. Style it with <c>Tooltip</c> rules, like any other Control. The text is a TextBlock, and the shortcut is a
/// TextBlock with the Class <c>shortcut</c>, so <c>Tooltip .shortcut</c> styles the shortcut alone. The Default theme sets
/// it in a monospace font, <c>--font-mono</c>, which starts with Fira Code.
/// </para>
/// <para>
/// With only text and a shortcut, the Tooltip is one row: the text, then the shortcut. As soon as there is a
/// <see cref="Header"/>, a <see cref="Description"/>, an <see cref="Icon"/> or a <see cref="Glyph"/>, it is laid out as the
/// icon on the left and, beside it, a column with the header (and the shortcut at its end), the text and the description.
/// Parts without content are left out. The parts have the Classes <c>icon</c>, <c>body</c>, <c>header-row</c>, <c>header</c>,
/// <c>text-row</c>, <c>text</c>, <c>shortcut</c> and <c>description</c>, so a Stylesheet decides their alignment and spacing
/// (<c>align-items</c> and <c>flex-direction</c> of <c>Tooltip</c>, <c>align-self</c> of <c>Tooltip .icon</c>). The description
/// wraps at its <c>max-width</c> (280px in the Default theme).
/// </para>
/// <para>
/// A <see cref="Content"/> replaces all of this. A Tooltip is shown as a passive overlay: the pointer goes through it, and it
/// never takes a press.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// save.Tooltip = "Write the document to disk";
/// save.TooltipHeader = "Save";
/// save.TooltipShortcut = "Ctrl+S";
/// save.TooltipGlyph = "check";
/// save.TooltipDescription = "Saves to the file the document was opened from. Unsaved changes are kept in a backup.";
/// </code>
/// </example>
public class Tooltip : Control
{
    /// <summary>The line of text.</summary>
    readonly TextBlock label = new();

    /// <summary>The shortcut, which is a child only while there is one.</summary>
    readonly TextBlock keys = new();

    /// <summary>The header.</summary>
    readonly TextBlock headerText = new();

    /// <summary>The description, which wraps.</summary>
    readonly DescriptionText description = new();

    /// <summary>The row with the header and the shortcut.</summary>
    readonly Box headerRow = new();

    /// <summary>The row with the text and the shortcut, used when there is no header.</summary>
    readonly Box textRow = new();

    /// <summary>The column beside the icon.</summary>
    readonly Box body = new();

    /// <summary>The icon when it is a picture.</summary>
    readonly Image iconImage = new();

    /// <summary>The icon when it is a glyph.</summary>
    readonly Icon iconGlyph = new();


    /// <summary>The Control that replaces the built-in layout.</summary>
    Control? content;

    /// <summary>The picture of the icon.</summary>
    IImage? icon;

    /// <summary>The name of the glyph of the icon.</summary>
    string glyph = string.Empty;


    /// <summary>
    /// Runs before the first Tooltip is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Tooltip()
    {
    }

    /// <summary>Creates an empty Tooltip.</summary>
    public Tooltip()
    {
        keys.AddClass("shortcut");
        label.AddClass("text");
        headerText.AddClass("header");
        description.AddClass("description");
        headerRow.AddClass("header-row");
        textRow.AddClass("text-row");
        body.AddClass("body");
        iconImage.AddClass("icon");
        iconGlyph.AddClass("icon");

        Rebuild();
    }


    /// <summary>Gets or sets the line of text. The default is an empty string.</summary>
    /// <remarks>Arrange the tree again after changing it, since the size of the Tooltip depends on it.</remarks>
    public string Text
    {
        get => label.Text;
        set => label.Text = value;
    }

    /// <summary>Gets or sets the shortcut shown after the text, such as <c>Ctrl+S</c>. Empty shows none.</summary>
    /// <remarks>Apply the Stylesheets again after changing it, since a new part needs its styles.</remarks>
    public string Shortcut
    {
        get => keys.Text;
        set
        {
            keys.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the bold title above the text. Empty shows none.</summary>
    /// <remarks>Apply the Stylesheets again after changing it.</remarks>
    public string Header
    {
        get => headerText.Text;
        set
        {
            headerText.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the longer explanation below the text, which wraps. Empty shows none.</summary>
    /// <remarks>Apply the Stylesheets again after changing it.</remarks>
    public string Description
    {
        get => description.Text;
        set
        {
            description.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>
    /// Gets or sets the picture shown on the left. A mask is drawn in the text color, any other picture keeps its colors.
    /// <see langword="null"/> shows none. It wins over <see cref="Glyph"/>.
    /// </summary>
    /// <remarks>Apply the Stylesheets again after changing it.</remarks>
    public IImage? Icon
    {
        get => icon;
        set
        {
            icon = value;
            iconImage.Source = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the name of a glyph from <see cref="Glyphs"/> to show on the left, drawn in the text color. Empty shows none.</summary>
    /// <remarks>Apply the Stylesheets again after changing it.</remarks>
    public string Glyph
    {
        get => glyph;
        set
        {
            glyph = value ?? string.Empty;
            iconGlyph.Glyph = glyph;

            Rebuild();
        }
    }

    /// <summary>Gets or sets a Control that is shown instead of all the other parts, or <see langword="null"/> to use them.</summary>
    /// <remarks>Apply the Stylesheets again after changing it. The Control stays the child of this Tooltip until the Tooltip is discarded.</remarks>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;

            Rebuild();
        }
    }

    /// <summary>Takes the custom <see cref="Content"/> out of the Tooltip, so that another Tooltip can show it. The presenter calls it when it hides a Tooltip.</summary>
    internal void ReleaseContent()
    {
        if (content is not null)
            Children.Clear();
    }

    /// <summary>Puts the parts that are in use into the Tooltip, in order.</summary>
    void Rebuild()
    {
        Children.Clear();
        body.Children.Clear();
        headerRow.Children.Clear();
        textRow.Children.Clear();

        if (content is not null)
        {
            Children.Add(content);

            return;
        }

        var hasHeader = headerText.Text.Length > 0;
        var hasKeys = keys.Text.Length > 0;
        var hasText = label.Text.Length > 0;
        var extended = hasHeader || description.Text.Length > 0 || icon is not null || glyph.Length > 0;

        if (!extended)
        {
            Children.Add(label);

            if (hasKeys)
                Children.Add(keys);

            return;
        }

        if (icon is not null)
            Children.Add(iconImage);
        else if (glyph.Length > 0)
            Children.Add(iconGlyph);

        if (hasHeader)
        {
            headerRow.Children.Add(headerText);

            if (hasKeys)
                headerRow.Children.Add(keys);

            body.Children.Add(headerRow);
        }

        if (hasText || (hasKeys && !hasHeader))
        {
            if (hasText)
                textRow.Children.Add(label);

            if (hasKeys && !hasHeader)
                textRow.Children.Add(keys);

            body.Children.Add(textRow);
        }

        if (description.Text.Length > 0)
            body.Children.Add(description);

        Children.Add(body);
    }

    /// <summary>A line of text that wraps at its <c>max-width</c>.</summary>
    sealed class DescriptionText : TextControl
    {
        /// <summary>Gets the type name that Selectors see, which is that of a plain TextBlock so that it takes the same styles.</summary>
        public override string TypeName => "TextBlock";

        /// <inheritdoc/>
        protected override Size MeasureContent(double? availableWidth)
        {
            var lines = Wrap();

            if (lines.Count == 0)
                return default;

            var style = CurrentTextStyle();

            return new Size(lines.Max(line => Measurer().Measure(line, style).Width), lines.Count * Measurer().Measure("M", style).Height);
        }

        /// <inheritdoc/>
        protected override void OnRender(IDrawingContext context, Rect bounds)
        {
            if (Text.Length == 0)
                return;

            var content = ContentBox(bounds);
            var style = CurrentTextStyle();
            var height = Measurer().Measure("M", style).Height;
            var y = content.Y;

            foreach (var line in Wrap())
            {
                context.DrawText(line, content.X, y, style, GetValue(ColorProperty));

                y += height;
            }
        }

        /// <summary>Breaks the text into lines no wider than <c>max-width</c>, at spaces. A word that is wider stays whole on its line.</summary>
        /// <returns>The lines.</returns>
        List<string> Wrap()
        {
            if (Text.Length == 0)
                return [];

            var limit = GetValue(MaxWidthProperty) is { } max ? Lengths.Resolve(max, 0) : null;
            var style = CurrentTextStyle();
            var lines = new List<string>();
            var current = string.Empty;

            foreach (var word in Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";

                if (current.Length > 0 && limit is { } width && Measurer().Measure(candidate, style).Width > width)
                {
                    lines.Add(current);

                    current = word;

                    continue;
                }

                current = candidate;
            }

            if (current.Length > 0)
                lines.Add(current);

            return lines;
        }
    }
}
