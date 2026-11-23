using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>
/// A small pill with a short text or a count, such as the number of unread messages.
/// </summary>
/// <remarks>
/// <para>
/// Set <c>Text</c> for a word, or <see cref="Value"/> for a count: a count above <see cref="Maximum"/> shows as, for
/// example, <c>99+</c>. A Badge with no text paints nothing, so it can stay in the layout and be switched on by setting the
/// text. <see cref="Severity"/> adds the Class <c>info</c>, <c>success</c>, <c>warning</c> or <c>error</c> for the theme to
/// color; without one the Badge is neutral.
/// </para>
/// <para>The pill shape and the padding come from the theme: <c>border-radius</c>, <c>padding</c> and <c>text-align</c>.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var unread = new Badge { Value = 120, Severity = Severity.Error };
///
/// // shows "99+"
/// </code>
/// </example>
public class Badge : TextControl
{
    /// <summary>The largest count that is shown as it is.</summary>
    int maximum = 99;

    /// <summary>The count, or <see langword="null"/> when the text was set directly.</summary>
    int? value;

    /// <summary>The kind.</summary>
    Severity? severity;


    /// <summary>
    /// Runs before the first Badge is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Badge()
    {
    }

    /// <summary>Gets or sets the count. <see langword="null"/> shows no text, and so nothing.</summary>
    public int? Value
    {
        get => value;
        set
        {
            this.value = value;

            Refresh();
        }
    }

    /// <summary>Gets or sets the largest count that is shown as it is. A larger one shows as the maximum and a plus sign. The default is 99.</summary>
    public int Maximum
    {
        get => maximum;
        set
        {
            maximum = Math.Max(0, value);

            Refresh();
        }
    }

    /// <summary>Gets or sets the kind of the Badge, which sets its Class. <see langword="null"/> (the default) is neutral and has none.</summary>
    public Severity? Severity
    {
        get => severity;
        set
        {
            if (severity is { } old)
                RemoveClass(old.ToString().ToLowerInvariant());

            severity = value;

            if (value is { } next)
                AddClass(next.ToString().ToLowerInvariant());
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth) => Text.Length == 0 ? default : base.MeasureContent(availableWidth);

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        if (Text.Length > 0)
            base.OnRender(context, bounds);
    }

    /// <summary>Writes the count into the text.</summary>
    void Refresh()
    {
        if (value is not { } count)
        {
            Text = string.Empty;

            return;
        }

        Text = count > maximum ? $"{maximum}+" : count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }
}
