using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>A pane of destinations at the side of an area that shows the chosen one.</summary>
/// <remarks>
/// <para>
/// The pane lists the <see cref="Items"/>: each destination is a button with an optional glyph, and a header is a small title
/// above a group. The pane is a column with the Class <c>pane</c>, a destination is a <c>Button</c> with the Class <c>item</c>
/// (and <c>selected</c> while chosen), and a header a TextBlock with the Class <c>header</c>. The area beside it, with the Class
/// <c>content</c>, holds <see cref="Content"/>, which the application swaps in <see cref="SelectionChanged"/>.
/// </para>
/// <para>
/// While <see cref="IsCompact"/> the pane shows only the glyphs, and the text becomes the tooltip of the destination. The
/// NavigationView then has the Class <c>compact</c>. <see cref="IsPaneOpen"/> hides the pane altogether. A back stack and the
/// creation of pages are the application's. Apply styles again after any change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var nav = new NavigationView { Items = [new("Home", "home"), new("Settings", "menu")] };
///
/// nav.SelectionChanged += (_, _) => nav.Content = Pages[nav.SelectedIndex];
/// nav.SelectedIndex = 0;
/// </code>
/// </example>
public class NavigationView : Control
{
    /// <summary>The pane.</summary>
    readonly Box pane = new();

    /// <summary>The area for the page.</summary>
    readonly Box contentHost = new();

    /// <summary>The buttons of the destinations, by index of <see cref="Items"/>; <see langword="null"/> for a header.</summary>
    readonly List<DestinationButton?> buttons = [];


    /// <summary>The entries.</summary>
    IReadOnlyList<NavigationItem> items = [];

    /// <summary>The index of the chosen destination, or -1.</summary>
    int selectedIndex = -1;

    /// <summary>Whether the pane is shown.</summary>
    bool isPaneOpen = true;

    /// <summary>Whether the pane shows only glyphs.</summary>
    bool isCompact;

    /// <summary>The page.</summary>
    Control? content;

    /// <summary>The width of the pane that was asked for.</summary>
    double paneWidth;


    /// <summary>
    /// Runs before the first NavigationView is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static NavigationView()
    {
    }

    /// <summary>Creates an empty NavigationView.</summary>
    public NavigationView()
    {
        pane.AddClass("pane");
        contentHost.AddClass("content");

        Rebuild();
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets or sets the entries. Setting it clears the selection.</summary>
    public IReadOnlyList<NavigationItem> Items
    {
        get => items;
        set
        {
            items = value ?? [];
            selectedIndex = -1;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the index in <see cref="Items"/> of the chosen destination, or -1. A header or a value outside the list selects nothing.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var next = value >= 0 && value < items.Count && !items[value].IsHeader ? value : -1;

            if (next == selectedIndex)
                return;

            if (selectedIndex >= 0)
                buttons[selectedIndex]?.RemoveClass("selected");

            selectedIndex = next;

            if (next >= 0)
                buttons[next]?.AddClass("selected");

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the chosen entry, or <see langword="null"/>.</summary>
    public NavigationItem? SelectedItem => selectedIndex >= 0 ? items[selectedIndex] : null;

    /// <summary>Gets or sets the page beside the pane, or <see langword="null"/> for none.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;

            contentHost.Children.Clear();

            if (value is not null)
                contentHost.Children.Add(value);
        }
    }

    /// <summary>Gets or sets whether the pane is shown. The default is <see langword="true"/>.</summary>
    public bool IsPaneOpen
    {
        get => isPaneOpen;
        set
        {
            isPaneOpen = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets whether the pane shows only the glyphs. The default is <see langword="false"/>.</summary>
    public bool IsCompact
    {
        get => isCompact;
        set
        {
            isCompact = value;

            if (value)
                AddClass("compact");
            else
                RemoveClass("compact");

            Rebuild();
        }
    }

    /// <summary>
    /// Gets or sets the width of the pane in pixels, which overrides the one from the theme. 0 (the default) leaves it to the theme.
    /// </summary>
    public double PaneWidth
    {
        get => paneWidth;
        set
        {
            paneWidth = Math.Max(0, value);

            if (paneWidth > 0)
                pane.SetValue(WidthProperty, new Length(paneWidth, LengthUnit.Px));
        }
    }

    /// <summary>Builds the pane from the entries, and the pane and the page into the NavigationView.</summary>
    void Rebuild()
    {
        pane.Children.Clear();
        buttons.Clear();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item.IsHeader)
            {
                buttons.Add(null);

                if (!isCompact)
                {
                    var header = new TextBlock { Text = item.Text };

                    header.AddClass("header");
                    pane.Children.Add(header);
                }

                continue;
            }

            var index = i;
            var button = new DestinationButton(item.Glyph) { Text = isCompact ? string.Empty : item.Text, Tooltip = isCompact ? item.Text : null };

            button.AddClass("item");

            if (i == selectedIndex)
                button.AddClass("selected");

            button.Click += (_, _) => SelectedIndex = index;

            buttons.Add(button);
            pane.Children.Add(button);
        }

        Children.Clear();

        if (isPaneOpen)
            Children.Add(pane);

        Children.Add(contentHost);
    }

    /// <summary>A destination: a button that draws its glyph in the space the padding leaves at the left.</summary>
    /// <param name="glyph">The name of the glyph.</param>
    sealed class DestinationButton(string glyph) : Button
    {
        /// <summary>The side of the glyph in pixels.</summary>
        const double GlyphSize = 18;

        /// <summary>Gets the type name that Selectors see, which is that of a plain Button so that it takes the same styles.</summary>
        public override string TypeName => "Button";

        /// <inheritdoc/>
        protected override void OnRender(IDrawingContext context, Rect bounds)
        {
            base.OnRender(context, bounds);

            if (glyph.Length == 0)
                return;

            var content = ContentBox(bounds);
            var x = Text.Length == 0 ? bounds.X + ((bounds.Width - GlyphSize) / 2) : (content.X - GlyphSize - 10);

            Icon.Draw(context, glyph, new Rect(Math.Max(0, x), (bounds.Height - GlyphSize) / 2, GlyphSize, GlyphSize), GetValue(ColorProperty), 2);
        }
    }
}
