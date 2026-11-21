using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// The base of Buttons that are either on or off and draw an indicator beside their text: CheckBox, RadioButton and ToggleSwitch.
/// </summary>
/// <remarks>
/// <para>
/// While a ToggleButton is on it has the <c>checked</c> Pseudo-state, so a Stylesheet can restyle it with <c>CheckBox:checked</c>.
/// The indicator is drawn at the left edge, vertically centered, and uses the <c>indicator-background</c>,
/// <c>indicator-border-color</c> and <c>indicator-mark-color</c> properties. The text starts after it, so give the Control
/// a <c>padding</c> with room on the left, such as <c>2px 2px 2px 24px</c>.
/// </para>
/// <para>The mark is drawn from rectangles, so it is a filled square or dot rather than a tick.</para>
/// </remarks>
public abstract class ToggleButton : Button
{
    /// <summary>The fill of the indicator.</summary>
    public static readonly StyledProperty<Color> IndicatorBackgroundProperty =
        StyledProperty.Register<ToggleButton, Color>("indicator-background", new(255, 255, 255), Color.TryParse);

    /// <summary>The outline of the indicator.</summary>
    public static readonly StyledProperty<Color> IndicatorBorderColorProperty =
        StyledProperty.Register<ToggleButton, Color>("indicator-border-color", new(156, 163, 175), Color.TryParse);

    /// <summary>The color of the mark that shows the Control is on.</summary>
    public static readonly StyledProperty<Color> IndicatorMarkColorProperty =
        StyledProperty.Register<ToggleButton, Color>("indicator-mark-color", new(255, 255, 255), Color.TryParse);

    /// <summary>Whether the Control is on.</summary>
    bool isChecked;

    /// <summary>
    /// Runs before the first ToggleButton is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ToggleButton()
    {
    }

    /// <summary>Raised after <see cref="IsChecked"/> changed.</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>Gets or sets whether the Control is on. Styles must be applied again after it changes.</summary>
    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
                return;

            isChecked = value;

            SetPseudoState("checked", value);
            OnCheckedChanged();

            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the width and height of the indicator's box in pixels.</summary>
    protected virtual Size IndicatorSize => new(16, 16);

    /// <summary>Gets the corner radius of the indicator in pixels.</summary>
    protected virtual double IndicatorRadius => 3;

    /// <summary>Runs after <see cref="IsChecked"/> changed and before <see cref="CheckedChanged"/> is raised.</summary>
    protected virtual void OnCheckedChanged()
    {
    }

    /// <summary>Gets the new value of <see cref="IsChecked"/> that a click produces.</summary>
    /// <returns>The value to set. The default flips the current one.</returns>
    protected virtual bool ValueAfterClick() => !isChecked;

    /// <inheritdoc/>
    protected override bool OnClick()
    {
        var before = isChecked;

        IsChecked = ValueAfterClick();

        return before != isChecked;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var size = IndicatorSize;
        var box = new Rect(0, Math.Max(0, (bounds.Height - size.Height) / 2), size.Width, size.Height);

        DrawIndicator(context, box);
    }

    /// <summary>Draws the indicator.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="box">The area of the indicator in the Control's own coordinates.</param>
    protected virtual void DrawIndicator(IDrawingContext context, Rect box)
    {
        context.FillRect(box, GetValue(IndicatorBackgroundProperty), IndicatorRadius);
        context.StrokeRect(box, GetValue(IndicatorBorderColorProperty), 1, IndicatorRadius);

        if (!isChecked)
            return;

        var inset = box.Width / 4;

        var mark = new Rect(box.X + inset, box.Y + inset, box.Width - inset * 2, box.Height - inset * 2);

        context.FillRect(mark, GetValue(IndicatorMarkColorProperty), IndicatorRadius / 2);
    }
}
