namespace Silo;

/// <summary>A connected strip of segments, exactly one of which is selected.</summary>
/// <remarks>
/// <para>
/// Every item is a <see cref="Button"/> with the Class <c>segment</c>; the selected one also has the Class <c>selected</c>, and
/// the first and last have <c>first</c> and <c>last</c>, so a Stylesheet can treat the ends differently (a corner radius applies to all four corners). The SegmentedControl
/// takes the keyboard focus, and then Left and Right (and Home and End) move the selection.
/// </para>
/// <para>Apply styles again after the selection or the items change.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var view = new SegmentedControl { Items = ["Day", "Week", "Month"], SelectedIndex = 1 };
///
/// view.SelectionChanged += (_, _) => calendar.Mode = view.SelectedIndex;
/// </code>
/// </example>
public class SegmentedControl : Control
{
    /// <summary>The items.</summary>
    IReadOnlyList<string> items = [];

    /// <summary>The index of the selected segment, or -1.</summary>
    int selectedIndex = -1;


    /// <summary>
    /// Runs before the first SegmentedControl is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static SegmentedControl()
    {
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets or sets the texts of the segments. Setting it clears the selection.</summary>
    public IReadOnlyList<string> Items
    {
        get => items;
        set
        {
            items = value ?? [];
            selectedIndex = -1;

            Children.Clear();

            for (var i = 0; i < items.Count; i++)
            {
                var index = i;
                var segment = new Button { Text = items[i] };

                segment.AddClass("segment");

                if (i == 0)
                    segment.AddClass("first");

                if (i == items.Count - 1)
                    segment.AddClass("last");

                segment.Click += (_, _) => SelectedIndex = index;

                Children.Add(segment);
            }
        }
    }

    /// <summary>Gets or sets the index of the selected segment, or -1 for none. Values outside the list select nothing.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var next = value >= 0 && value < items.Count ? value : -1;

            if (next == selectedIndex)
                return;

            if (selectedIndex >= 0)
                ((Control)Children[selectedIndex]).RemoveClass("selected");

            selectedIndex = next;

            if (next >= 0)
                ((Control)Children[next]).AddClass("selected");

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = selectedIndex;

        switch (key)
        {
            case Key.Left:
                SelectedIndex = Math.Max(0, selectedIndex - 1);
                break;

            case Key.Right:
                SelectedIndex = Math.Min(items.Count - 1, selectedIndex + 1);
                break;

            case Key.Home:
                SelectedIndex = 0;
                break;

            case Key.End:
                SelectedIndex = items.Count - 1;
                break;
        }

        return before != selectedIndex;
    }
}
