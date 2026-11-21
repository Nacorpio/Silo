using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// Shows the selected one of a list of texts, and drops down the list so the user can pick another.
/// </summary>
/// <remarks>
/// <para>
/// Pressing the ComboBox opens the list in the window's <see cref="OverlayLayer"/>, right below it and as wide as the
/// ComboBox. A press on a row selects it and closes the list, a press anywhere else or Escape closes it unchanged. With the
/// keyboard focus, Enter opens it, Up, Down, Home and End move through the rows, and Enter picks one. While it is closed, Up
/// and Down change the selection directly.
/// </para>
/// <para>
/// The list shows at most <c>max-rows</c> rows (8 by default) of <c>item-height</c> (28px by default), and scrolls when there
/// are more. Without a <c>width</c> the ComboBox is as wide as its widest text. The list is not moved to fit a window that
/// is too small for it.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var size = new ComboBox { Items = ["Small", "Medium", "Large"], SelectedIndex = 1 };
///
/// size.SelectionChanged += (_, _) => Resize(size.SelectedItem);
/// </code>
/// </example>
public class ComboBox : Control
{
    /// <summary>The room right of the text for the arrow in pixels.</summary>
    const double ArrowSpace = 24;

    /// <summary>The number of rows that the list scrolls beyond when <c>max-rows</c> is not set.</summary>
    const int DefaultMaxRows = 8;


    /// <summary>The height of each row in the list in pixels.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        StyledProperty.Register<ComboBox, double>("item-height", 28, ValueParsers.Pixels);

    /// <summary>The most rows the list shows before it scrolls.</summary>
    public static readonly StyledProperty<double> MaxRowsProperty =
        StyledProperty.Register<ComboBox, double>("max-rows", DefaultMaxRows, ValueParsers.Number);


    /// <summary>The choices.</summary>
    IReadOnlyList<string> items = [];

    /// <summary>The index of the selected choice, or -1.</summary>
    int selectedIndex = -1;

    /// <summary>The open list, or <see langword="null"/>.</summary>
    ListView? popup;


    /// <summary>
    /// Runs before the first ComboBox is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ComboBox()
    {
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;


    /// <summary>Gets or sets the choices. Setting it clears the selection and closes the list.</summary>
    public IReadOnlyList<string> Items
    {
        get => items;
        set
        {
            Close();

            items = value ?? [];

            SelectedIndex = -1;
        }
    }

    /// <summary>Gets or sets the index of the selected choice, or -1 for none. Values outside the list select nothing.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var next = value >= 0 && value < items.Count ? value : -1;

            if (next == selectedIndex)
                return;

            selectedIndex = next;

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the selected choice, or <see langword="null"/> for none.</summary>
    public string? SelectedItem => selectedIndex >= 0 ? items[selectedIndex] : null;

    /// <summary>Gets whether the list is open.</summary>
    public bool IsOpen => popup is not null && Overlays?.Contains(popup) == true;

    /// <inheritdoc/>
    public override bool IsFocusable => true;


    /// <summary>Opens the list below the ComboBox.</summary>
    /// <returns>
    /// <see langword="true"/> when it opened. It does not when it is open already, when there are no choices, or when the
    /// ComboBox is not in a window.
    /// </returns>
    /// <remarks>Call it after <see cref="Control.Arrange"/>, since the list sits where the ComboBox was arranged.</remarks>
    public bool Open()
    {
        if (IsOpen || items.Count == 0 || Overlays is not { } layer)
            return false;

        var (left, top) = ToLocal(0, 0);
        var height = GetValue(ItemHeightProperty);
        var rows = Math.Min(items.Count, Math.Max(1, (int)GetValue(MaxRowsProperty)));

        popup = new DropDownList { Items = items };
        popup.SelectedIndex = selectedIndex;

        popup.SetValue(ListView.ItemHeightProperty, height);
        popup.SetValue(WidthProperty, new Length(Bounds.Width, LengthUnit.Px));
        popup.SetValue(HeightProperty, new Length(rows * height + 2, LengthUnit.Px));
        popup.SetValue(PaddingProperty, new Thickness(Px(0), Px(0), Px(0), Px(0)));
        popup.SetValue(BorderWidthProperty, new Thickness(Px(1), Px(1), Px(1), Px(1)));
        popup.Activated += (_, index) => Choose(index);

        layer.Show(popup, -left, -top + Bounds.Height);

        return true;
    }

    /// <summary>Closes the list without changing the selection.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        var list = popup;

        popup = null;

        return list is not null && Overlays?.Close(list) == true;
    }

    /// <summary>Opens the list when it is closed. The pointer router calls this when the pointer goes down on the ComboBox.</summary>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    /// <remarks>A press while the list is open never gets here: the overlay layer closes the list first.</remarks>
    public bool PointerDown() => Open();

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        if (popup is not null && IsOpen)
            return KeyDownOpen(key);

        switch (key)
        {
            case Key.Enter:
                return Open();

            case Key.Up:
                SelectedIndex = Math.Max(0, selectedIndex - 1);

                return true;

            case Key.Down:
                SelectedIndex = Math.Min(items.Count - 1, selectedIndex + 1);

                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var width = items.Select(item => measurer.Measure(item, style).Width).DefaultIfEmpty(0).Max();

        return new Size(width + ArrowSpace, measurer.Measure("Ag", style).Height);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var color = GetValue(ColorProperty);
        var text = SelectedItem ?? string.Empty;
        var y = content.Y + Math.Max(0, content.Height - Measurer().Measure("Ag", style).Height) / 2;

        context.Save();
        context.ClipRect(new Rect(content.X, content.Y, Math.Max(0, content.Width - ArrowSpace), content.Height));
        context.DrawText(text, content.X, y, style, color);
        context.Restore();

        DrawArrow(context, new Rect(content.Right - ArrowSpace, content.Y, ArrowSpace, content.Height), color);
    }

    /// <summary>Handles a key while the list is open.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    bool KeyDownOpen(Key key)
    {
        if (key != Key.Enter)
            return popup!.KeyDown(key);

        if (popup!.SelectedIndex >= 0)
            Choose(popup.SelectedIndex);

        return true;
    }

    /// <summary>Selects a choice and closes the list.</summary>
    /// <param name="index">The choice's index.</param>
    void Choose(int index)
    {
        Close();

        SelectedIndex = index;
    }

    /// <summary>Draws the small downward triangle.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="area">The space reserved for the arrow.</param>
    /// <param name="color">The color to draw it in.</param>
    static void DrawArrow(IDrawingContext context, Rect area, Color color)
    {
        const int rows = 4;

        var top = area.Y + (area.Height - rows) / 2;

        for (var i = 0; i < rows; i++)
        {
            var width = 8 - i * 2;
            var row = new Rect(area.X + (area.Width - width) / 2, top + i, width, 1);

            context.FillRect(row, color);
        }
    }

    /// <summary>Makes a length in pixels.</summary>
    /// <param name="value">The number of pixels.</param>
    /// <returns>The length.</returns>
    static Length Px(double value) => new(value, LengthUnit.Px);


    /// <summary>The list of choices. It never takes the keyboard focus, which stays on the ComboBox.</summary>
    sealed class DropDownList : ListView
    {
        /// <inheritdoc/>
        public override string TypeName => "ListView";

        /// <inheritdoc/>
        public override bool IsFocusable => false;
    }
}
