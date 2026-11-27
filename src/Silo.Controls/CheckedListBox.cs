using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>A <see cref="ListView"/> whose rows each have a check box, so that any number of rows can be checked.</summary>
/// <remarks>
/// <para>
/// Pressing a row, or pressing Enter or Space on the selected row, flips its check. Selecting a row, which the arrow keys do,
/// does not. The boxes are drawn with <c>check-color</c> (the fill of a checked box), <c>check-border-color</c> and
/// <c>check-mark-color</c>. Everything else works as in <see cref="ListView"/>: give it a <c>height</c>, and set the rows with
/// <c>Items</c>, which also clears the checks.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var toppings = new CheckedListBox { Items = ["Cheese", "Olives", "Onions"] };
///
/// toppings.SetChecked(0, true);
/// toppings.CheckedChanged += (_, _) => order.Toppings = toppings.CheckedIndices;
/// </code>
/// </example>
[PseudoStates("focus")]
public class CheckedListBox : ListView
{
    /// <summary>The side of a check box in pixels.</summary>
    const double BoxSize = 16;

    /// <summary>The fill of a checked box.</summary>
    public static readonly StyledProperty<Color> CheckColorProperty =
        StyledProperty.Register<CheckedListBox, Color>("check-color", new(59, 130, 246), Color.TryParse);

    /// <summary>The outline of a box.</summary>
    public static readonly StyledProperty<Color> CheckBorderColorProperty =
        StyledProperty.Register<CheckedListBox, Color>("check-border-color", new(156, 163, 175), Color.TryParse);

    /// <summary>The color of the tick in a checked box.</summary>
    public static readonly StyledProperty<Color> CheckMarkColorProperty =
        StyledProperty.Register<CheckedListBox, Color>("check-mark-color", new(255, 255, 255), Color.TryParse);


    /// <summary>The indices of the checked rows.</summary>
    readonly SortedSet<int> checkedRows = [];


    /// <summary>
    /// Runs before the first CheckedListBox is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static CheckedListBox()
    {
    }

    /// <summary>Creates an empty CheckedListBox.</summary>
    public CheckedListBox()
    {
        Activated += (_, index) => SetChecked(index, !IsChecked(index));
    }

    /// <summary>Raised after the checks changed.</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>Gets or sets the rows. Setting it unchecks all of them, clears the selection and scrolls to the top.</summary>
    public new IReadOnlyList<string> Items
    {
        get => base.Items;
        set
        {
            var any = checkedRows.Count > 0;

            checkedRows.Clear();

            base.Items = value;

            if (any)
                CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the indices of the checked rows in ascending order.</summary>
    public IReadOnlyList<int> CheckedIndices => [.. checkedRows];

    /// <summary>Tells whether a row is checked.</summary>
    /// <param name="index">The row's index.</param>
    /// <returns><see langword="true"/> when the row is checked.</returns>
    public bool IsChecked(int index) => checkedRows.Contains(index);

    /// <summary>Checks or unchecks a row.</summary>
    /// <param name="index">The row's index. A value outside the list does nothing.</param>
    /// <param name="value"><see langword="true"/> to check the row.</param>
    public void SetChecked(int index, bool value)
    {
        if (index < 0 || index >= Items.Count)
            return;

        if (value ? !checkedRows.Add(index) : !checkedRows.Remove(index))
            return;

        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Checks every row, or none.</summary>
    /// <param name="value"><see langword="true"/> to check all rows, <see langword="false"/> to uncheck them.</param>
    public void SelectAll(bool value = true)
    {
        var before = checkedRows.Count;

        checkedRows.Clear();

        if (value)
            for (var i = 0; i < Items.Count; i++)
                checkedRows.Add(i);

        if (before != checkedRows.Count || value)
            CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public override bool TextInput(string text)
    {
        if (text != " " || SelectedIndex < 0)
            return false;

        SetChecked(SelectedIndex, !IsChecked(SelectedIndex));

        return true;
    }

    /// <inheritdoc/>
    protected override double RowIndent(int index) => BoxSize + IconGap;

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var viewport = Viewport;
        var first = (int)Math.Floor(Offset / RowHeight);
        var last = Math.Min(Items.Count - 1, (int)Math.Floor((Offset + viewport.Height) / RowHeight));

        context.Save();
        context.ClipRect(viewport);

        for (var i = first; i <= last; i++)
            DrawBox(context, new Rect(viewport.X + TextInset, RowTop(i) + ((RowHeight - BoxSize) / 2), BoxSize, BoxSize), IsChecked(i));

        context.Restore();
    }

    /// <summary>Draws one check box.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="box">The area of the box.</param>
    /// <param name="isChecked">Whether the box is checked.</param>
    void DrawBox(IDrawingContext context, Rect box, bool isChecked)
    {
        var border = GetValue(CheckBorderColorProperty);

        if (!isChecked)
        {
            context.StrokeRect(box, border, 1, 3);

            return;
        }

        var fill = GetValue(CheckColorProperty);

        context.FillRect(box, fill, 3);

        if (!Glyphs.TryGet("check", out var lines))
            return;

        var scale = (BoxSize - 4) / Glyphs.Grid;

        foreach (var line in lines)
            context.StrokePolyline([.. line.Points.Select(p => (box.X + 2 + (p.X * scale), box.Y + 2 + (p.Y * scale)))], GetValue(CheckMarkColorProperty), 1.75, line.Closed);
    }
}
