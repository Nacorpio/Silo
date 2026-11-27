using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// The clickable header of an <see cref="Expander"/>: its title with a triangle that points right while the Expander is
/// closed and down while it is open.
/// </summary>
/// <remarks>
/// <para>
/// The triangle is drawn at the left edge in the <c>indicator-color</c>, so give the header a <c>padding</c> with room on the
/// left, such as <c>4px 4px 4px 20px</c>. The header has the <c>expanded</c> Pseudo-state while its Expander is open.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var expander = new Expander { Header = "Advanced" };
///
/// expander.HeaderControl.AddClass("section");
/// </code>
/// </example>
[PseudoStates("expanded")]
public sealed class ExpanderHeader : Button
{
    /// <summary>The size of a step of the triangle in pixels.</summary>
    const double Step = 2;

    /// <summary>The color of the triangle.</summary>
    public static readonly StyledProperty<Color> IndicatorColorProperty =
        StyledProperty.Register<ExpanderHeader, Color>("indicator-color", new(107, 114, 128), Color.TryParse);


    /// <summary>Whether the Expander is open.</summary>
    bool isExpanded;


    /// <summary>
    /// Runs before the first ExpanderHeader is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ExpanderHeader()
    {
    }

    /// <summary>Gets or sets whether the Expander is open, which decides where the triangle points.</summary>
    /// <remarks>The Expander sets it. Styles must be applied again after it changes.</remarks>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            isExpanded = value;

            SetPseudoState("expanded", value);
        }
    }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    protected override bool OnClick() => true;

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var color = GetValue(IndicatorColorProperty);
        var left = 6.0;
        var middle = bounds.Height / 2;

        for (var i = 0; i < 3; i++)
        {
            var length = (5 - (2 * i)) * Step;

            context.FillRect(
                isExpanded
                    ? new Rect(left + (i * Step), middle - (1.5 * Step) + (i * Step), length, Step)
                    : new Rect(left + (i * Step), middle - (length / 2), Step, length),
                color);
        }
    }
}
