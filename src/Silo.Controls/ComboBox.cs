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
/// ComboBox, with the row under the pointer highlighted. A press on a row selects it and closes the list, a press anywhere else or Escape closes it unchanged. With the
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
[PseudoStates("hover", "focus")]
public class ComboBox : Control, IPointerHandler
{
    /// <summary>The number of rows that the list scrolls beyond when <c>max-rows</c> is not set.</summary>
    const int DefaultMaxRows = 8;


    /// <summary>The height of each row in the list in pixels.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        StyledProperty.Register<ComboBox, double>("item-height", 28, ValueParsers.Pixels);

    /// <summary>The most rows the list shows before it scrolls.</summary>
    public static readonly StyledProperty<double> MaxRowsProperty =
        StyledProperty.Register<ComboBox, double>("max-rows", DefaultMaxRows, ValueParsers.Number);


    /// <summary>Opens the list below the ComboBox and tracks whether it is open.</summary>
    readonly AnchoredPopup popup;


    /// <summary>The choices.</summary>
    IReadOnlyList<string> items = [];

    /// <summary>The index of the selected choice, or -1.</summary>
    int selectedIndex = -1;

    /// <summary>The open list, or <see langword="null"/>.</summary>
    ListView? list;


    /// <summary>
    /// Runs before the first ComboBox is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static ComboBox()
    {
    }

    /// <summary>Creates a ComboBox without choices.</summary>
    public ComboBox() => popup = new AnchoredPopup(this);

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
    public bool IsOpen => popup.IsOpen;

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
        if (IsOpen || items.Count == 0)
            return false;

        var height = GetValue(ItemHeightProperty);
        var rows = Math.Min(items.Count, Math.Max(1, (int)GetValue(MaxRowsProperty)));
        var dropDown = new DropDownList { Items = items, HoverSelects = true };

        dropDown.SelectedIndex = selectedIndex;

        dropDown.SetValue(ListView.ItemHeightProperty, height);
        dropDown.SetValue(HeightProperty, new Length(rows * height + 2, LengthUnit.Px));
        dropDown.SetValue(PaddingProperty, new Thickness(Px(0), Px(0), Px(0), Px(0)));
        dropDown.SetValue(BorderWidthProperty, new Thickness(Px(1), Px(1), Px(1), Px(1)));
        dropDown.Activated += (_, index) => Choose(index);

        if (!popup.Open(dropDown, matchWidth: true))
            return false;

        list = dropDown;

        return true;
    }

    /// <summary>Closes the list without changing the selection.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        list = null;

        return popup.Close();
    }

    /// <summary>Opens the list when it is closed. The pointer router calls this when the pointer goes down on the ComboBox.</summary>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    /// <remarks>A press while the list is open never gets here: the overlay layer closes the list first.</remarks>
    public bool PointerDown(PointerEvent e) => Open();

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        if (IsOpen)
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

        return new Size(width + DropDownArrow.Space, measurer.Measure("Ag", style).Height);
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
        context.ClipRect(new Rect(content.X, content.Y, Math.Max(0, content.Width - DropDownArrow.Space), content.Height));
        context.DrawText(text, content.X, y, style, color);
        context.Restore();

        DropDownArrow.Draw(context, content, color);
    }

    /// <summary>Handles a key while the list is open.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    bool KeyDownOpen(Key key)
    {
        if (key != Key.Enter)
            return popup.KeyDown(key, KeyModifiers.None);

        if (list!.SelectedIndex >= 0)
            Choose(list.SelectedIndex);

        return true;
    }

    /// <summary>Selects a choice and closes the list.</summary>
    /// <param name="index">The choice's index.</param>
    void Choose(int index)
    {
        Close();

        SelectedIndex = index;
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
