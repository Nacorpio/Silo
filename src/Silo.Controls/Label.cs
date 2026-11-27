using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A <see cref="TextBlock"/> that names another Control.
/// </summary>
/// <remarks>
/// <para>
/// Pressing the Label acts on <see cref="For"/>: a CheckBox, RadioButton or ToggleSwitch is toggled as if it was pressed, and
/// any other Control that can have the keyboard focus gets it. A Label without a target is plain text.
/// </para>
/// <para>
/// With the Class <c>required</c> the Label ends in a marker, an asterisk in the <c>required-color</c>, to show that the
/// Control it names must be filled in.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var name = new TextBox();
/// var label = new Label { Text = "Name", For = name };
///
/// label.AddClass("required");
/// </code>
/// </example>
public class Label : TextBlock, IPointerHandler
{
    /// <summary>The marker that follows the text of a required Label.</summary>
    const string Marker = "*";

    /// <summary>The space between the text and the marker in pixels.</summary>
    const double MarkerGap = 2;

    /// <summary>The color of the marker of a required Label.</summary>
    public static readonly StyledProperty<Color> RequiredColorProperty =
        StyledProperty.Register<Label, Color>("required-color", new(220, 38, 38), Color.TryParse);


    /// <summary>
    /// Runs before the first Label is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Label()
    {
    }

    /// <summary>Gets or sets the Control that the Label names. The default is <see langword="null"/>.</summary>
    public Control? For { get; set; }

    /// <summary>Gets whether the Label has the Class <c>required</c>.</summary>
    public bool IsRequired => Classes.Contains("required");

    /// <summary>Acts on <see cref="For"/>. The pointer router calls this when the pointer goes down on the Label.</summary>
    /// <param name="e">The pointer position relative to the Label's border box.</param>
    /// <returns><see langword="true"/> when the target changed, so styles need to be applied again.</returns>
    /// <remarks>A target that can have the focus gets it from the <see cref="FocusManager"/>, which looks at <see cref="For"/> too.</remarks>
    public bool PointerDown(PointerEvent e)
    {
        if (For is not ToggleButton toggle)
            return false;

        var changed = toggle.PointerDown();

        return toggle.PointerUp(inside: true) | changed;
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var size = base.MeasureContent(availableWidth);

        if (!IsRequired)
            return size;

        var marker = Measurer().Measure(Marker, CurrentTextStyle());

        return new Size(size.Width + MarkerGap + marker.Width, Math.Max(size.Height, marker.Height));
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (!IsRequired)
            return;

        var style = CurrentTextStyle();
        var (x, y) = TextPosition(bounds);
        var width = Text.Length == 0 ? 0 : Measurer().Measure(Text, style).Width;

        context.DrawText(Marker, x + width + MarkerGap, y, style, GetValue(RequiredColorProperty));
    }
}
