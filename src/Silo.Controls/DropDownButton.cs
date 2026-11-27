using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// A Button that opens a <see cref="ContextMenu"/> below itself when it is pressed, instead of doing something by itself.
/// </summary>
/// <remarks>
/// <para>
/// Give it the entries with <see cref="Entries"/>, or a ready menu with <see cref="Menu"/>. A small arrow after the text shows that
/// there is a menu. Pressing the button opens the menu below it, a press anywhere else or Escape closes it, and picking an entry
/// runs that entry's <see cref="MenuItem.Click"/>. A DropDownButton without text shows only the arrow, which is how
/// <see cref="SplitButton"/> uses it. It is styled as a Button, with the Class <c>dropdown</c> added.
/// </para>
/// <para>The menu is placed below <see cref="Anchor"/>, which is the button itself unless set.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var export = new DropDownButton
/// {
///     Text = "Export",
///     Entries = [new MenuItem("As &amp;PDF", ExportPdf), new MenuItem("As &amp;CSV", ExportCsv)],
/// };
/// </code>
/// </example>
[PseudoStates("hover", "active")]
public class DropDownButton : Button
{
    /// <summary>The size of the arrow in pixels.</summary>
    const double ArrowSize = 12;

    /// <summary>The room between the text and the arrow in pixels.</summary>
    const double ArrowGap = 6;

    /// <summary>The menu that opens.</summary>
    ContextMenu menu = new();

    /// <summary>Creates a DropDownButton with an empty menu.</summary>
    public DropDownButton() => AddClass("dropdown");

    /// <summary>Gets <c>Button</c>, so that the theme's Button Rules apply.</summary>
    public override string TypeName => "Button";

    /// <summary>Gets or sets the menu that opens. The default is an empty menu, which does not open.</summary>
    public ContextMenu Menu
    {
        get => menu;
        set => menu = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the entries of <see cref="Menu"/>.</summary>
    public IReadOnlyList<MenuItem> Entries
    {
        get => menu.Entries;
        set => menu.Entries = value;
    }

    /// <summary>Gets or sets the Control that the menu opens below. The default is <see langword="null"/>, the button itself.</summary>
    public Control? Anchor { get; set; }

    /// <summary>Gets whether the menu is open.</summary>
    public bool IsOpen => menu.IsOpen;

    /// <summary>Opens the menu below the button, unless it is open or has no entries.</summary>
    /// <returns><see langword="true"/> when it opened.</returns>
    public bool Open()
    {
        var (x, y) = AnchoredPopup.Below(Anchor ?? this);

        return menu.Show(this, x, y);
    }

    /// <inheritdoc/>
    protected override bool OnClick() => Open();

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var text = base.MeasureContent(availableWidth);

        return new Size(text.Width + (text.Width > 0 ? ArrowGap : 0) + ArrowSize, Math.Max(text.Height, ArrowSize));
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var square = new Rect(content.Right - ArrowSize, content.Y + ((content.Height - ArrowSize) / 2), ArrowSize, ArrowSize);

        Icon.Draw(context, "chevron-down", square, GetValue(ColorProperty), 2);
    }
}
