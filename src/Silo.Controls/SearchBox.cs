using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A <see cref="TextBox"/> for entering a search: it shows "Search" while empty and has a clear button.
/// </summary>
/// <remarks>
/// <para>
/// While there is text, a <c>×</c> is drawn at the right edge in the <c>placeholder-color</c>. Pressing it, or pressing Escape
/// with the keyboard focus, clears the text and raises <see cref="TextBox.TextChanged"/>. Enter raises
/// <see cref="TextBox.Submitted"/> as in any TextBox. The theme leaves room for the button with its right padding.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var search = new SearchBox();
///
/// search.TextChanged += (_, _) => results.Filter(search.Text);
/// </code>
/// </example>
public class SearchBox : TextBox
{
    /// <summary>The width of the clear button's hit area from the right edge, in pixels.</summary>
    const double ClearWidth = 24;


    /// <summary>Creates a SearchBox with "Search" as its placeholder.</summary>
    public SearchBox() => Placeholder = "Search";

    /// <inheritdoc/>
    public override bool PointerDown(PointerEvent e)
    {
        if (Text.Length > 0 && e.X >= Bounds.Width - ClearWidth)
        {
            Text = string.Empty;

            return true;
        }

        return base.PointerDown(e);
    }

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        if (key != Key.Escape || Text.Length == 0)
            return base.KeyDown(key);

        Text = string.Empty;

        return true;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (Text.Length == 0)
            return;

        var style = CurrentTextStyle();
        var glyph = Measurer().Measure("×", style);
        var x = bounds.Right - ClearWidth + ((ClearWidth - glyph.Width) / 2);
        var y = bounds.Y + Math.Max(0, bounds.Height - glyph.Height) / 2;

        context.DrawText("×", x, y, style, GetValue(PlaceholderColorProperty));
    }
}
