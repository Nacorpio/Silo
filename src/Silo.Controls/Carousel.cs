namespace Silo;

/// <summary>Shows one item of a list at a time, with buttons for the previous and the next and a dot for each item.</summary>
/// <remarks>
/// <para>
/// The current item sits in an area with the Class <c>viewport</c>, between the buttons <c>prev</c> and <c>next</c>, in a row
/// with the Class <c>stage</c>. Under it is a row <c>dots</c> with a Button <c>dot</c> for each item, the current one also with
/// <c>current</c>. Style the dots as small circles from the theme: they have no text.
/// </para>
/// <para>
/// With <see cref="IsLooping"/> the next of the last item is the first, and the previous of the first is the last. The Carousel
/// takes the keyboard focus, and Left and Right change the item. The item changes at once; sliding animations arrive with the
/// animation system. Apply styles again after any change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var slides = new Carousel { Items = [photoA, photoB, photoC], IsLooping = true };
///
/// slides.SelectionChanged += (_, _) => caption.Text = titles[slides.SelectedIndex];
/// </code>
/// </example>
public class Carousel : Control
{
    /// <summary>The row with the buttons and the item.</summary>
    readonly Box stage = new();

    /// <summary>The area for the current item.</summary>
    readonly Box viewport = new();

    /// <summary>The row of dots.</summary>
    readonly Box dots = new();

    /// <summary>The button for the previous item.</summary>
    readonly Button previous = new() { Text = "‹" };

    /// <summary>The button for the next item.</summary>
    readonly Button next = new() { Text = "›" };


    /// <summary>The items.</summary>
    IReadOnlyList<Control> items = [];

    /// <summary>The index of the current item, or -1 when there are none.</summary>
    int selectedIndex = -1;


    /// <summary>
    /// Runs before the first Carousel is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Carousel()
    {
    }

    /// <summary>Creates a Carousel with no items.</summary>
    public Carousel()
    {
        stage.AddClass("stage");
        viewport.AddClass("viewport");
        dots.AddClass("dots");
        previous.AddClass("prev");
        next.AddClass("next");

        previous.Click += (_, _) => Move(-1);
        next.Click += (_, _) => Move(1);

        stage.Children.Add(previous);
        stage.Children.Add(viewport);
        stage.Children.Add(next);

        Children.Add(stage);
        Children.Add(dots);
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets or sets the items. Setting it shows the first one.</summary>
    public IReadOnlyList<Control> Items
    {
        get => items;
        set
        {
            items = value ?? [];
            selectedIndex = items.Count > 0 ? 0 : -1;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the index of the current item, or -1 when there are none. Values outside the list are moved into range.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var target = items.Count == 0 ? -1 : Math.Clamp(value, 0, items.Count - 1);

            if (target == selectedIndex)
                return;

            selectedIndex = target;

            Rebuild();

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets whether the next of the last item is the first, and the previous of the first is the last. The default is <see langword="false"/>.</summary>
    public bool IsLooping { get; set; }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = selectedIndex;

        switch (key)
        {
            case Key.Left:
                Move(-1);
                break;

            case Key.Right:
                Move(1);
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

    /// <summary>Moves the current item.</summary>
    /// <param name="delta">-1 for the previous, 1 for the next.</param>
    void Move(int delta)
    {
        if (items.Count == 0)
            return;

        var target = selectedIndex + delta;

        if (IsLooping)
            target = (target + items.Count) % items.Count;

        SelectedIndex = target;
    }

    /// <summary>Shows the current item and builds the dots.</summary>
    void Rebuild()
    {
        viewport.Children.Clear();
        dots.Children.Clear();

        if (selectedIndex >= 0)
            viewport.Children.Add(items[selectedIndex]);

        for (var i = 0; i < items.Count; i++)
        {
            var dot = new Button();
            var index = i;

            dot.AddClass("dot");

            if (i == selectedIndex)
                dot.AddClass("current");

            dot.Click += (_, _) => SelectedIndex = index;

            dots.Children.Add(dot);
        }
    }
}
