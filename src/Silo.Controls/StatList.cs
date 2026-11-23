using Silo.Layout;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A column of rows that each show an icon, a bold value and a muted label, like the repository numbers in the sidebar of a
/// GitHub page: "1 star", "0 watching".
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="StatItem"/> becomes a <see cref="StatRow"/> with the classes <c>stat</c>, and <c>value</c> and <c>label</c>
/// on its texts. A row with no value is just an icon and text. Rows can be clicked, with a hover highlight, and raise
/// <see cref="ItemClicked"/>. The icon is a glyph drawn in the muted color, or the item's image.
/// </para>
/// <para>
/// The styled properties are <c>icon-size</c>, <c>stat-gap</c> (between rows) and <c>value-color</c> (the bold part).
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var stats = new StatList();
///
/// stats.Items = [new("activity", Label: "Activity"), new("star", "1", "star"), new("fork", "0", "forks")];
/// stats.ItemClicked += (_, index) => Open(stats.Items[index]);
/// </code>
/// </example>
public class StatList : Control
{
    /// <summary>The side of the square that icons fit in, in pixels.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        StyledProperty.Register<StatList, double>("icon-size", 16, ValueParsers.Pixels);

    /// <summary>The space between rows in pixels.</summary>
    public static readonly StyledProperty<double> StatGapProperty =
        StyledProperty.Register<StatList, double>("stat-gap", 8, ValueParsers.Pixels);

    /// <summary>The color of the values.</summary>
    public static readonly StyledProperty<Color> ValueColorProperty =
        StyledProperty.Register<StatList, Color>("value-color", new(31, 41, 55), Color.TryParse);


    /// <summary>The rows.</summary>
    IReadOnlyList<StatItem> items = [];


    /// <summary>
    /// Runs before the first StatList is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static StatList()
    {
    }

    /// <summary>Raised when a row is clicked. The argument is the index in <see cref="Items"/>.</summary>
    public event EventHandler<int>? ItemClicked;

    /// <summary>Gets or sets the rows.</summary>
    public IReadOnlyList<StatItem> Items
    {
        get => items;
        set
        {
            items = value ?? [];

            Rebuild();
        }
    }

    /// <inheritdoc/>
    protected override FlexDirection LayoutDirection => FlexDirection.Column;

    /// <inheritdoc/>
    protected override void OnStyled(Silo.Styling.StyleResolver resolver)
    {
        SetValue(GapProperty, new Length(Math.Max(0, GetValue(StatGapProperty)), LengthUnit.Px));

        foreach (var child in Children)
            Style((Control)child);
    }

    /// <summary>Builds a row for each item.</summary>
    void Rebuild()
    {
        Children.Clear();

        for (var i = 0; i < items.Count; i++)
            Children.Add(CreateRow(i));
    }

    /// <summary>Creates the row of an item.</summary>
    /// <param name="index">The item's index.</param>
    /// <returns>The row.</returns>
    StatRow CreateRow(int index)
    {
        var item = items[index];
        var row = new StatRow();

        row.AddClass("stat");
        row.Click += (_, _) => ItemClicked?.Invoke(this, index);

        if (item.Image is not null)
            row.Children.Add(new Image { Source = item.Image });
        else if (item.Glyph.Length > 0)
            row.Children.Add(new Icon { Glyph = item.Glyph });

        if (item.Value.Length > 0)
            row.Children.Add(Text(item.Value, "value"));

        if (item.Label.Length > 0)
            row.Children.Add(Text(item.Label, "label"));

        return row;
    }

    /// <summary>Makes one of a row's texts.</summary>
    /// <param name="text">The text.</param>
    /// <param name="name">The class.</param>
    /// <returns>The TextBlock.</returns>
    static TextBlock Text(string text, string name)
    {
        var block = new TextBlock { Text = text };

        block.AddClass(name);

        return block;
    }

    /// <summary>Gives a row's icon and value the styled values of the list.</summary>
    /// <param name="row">The row.</param>
    void Style(Control row)
    {
        var size = Math.Max(0, GetValue(IconSizeProperty));

        foreach (var part in row.Children.Cast<Control>())
        {
            if (part is Icon icon)
                icon.SetValue(Icon.IconSizeProperty, size);

            if (part is Image image)
            {
                image.SetValue(WidthProperty, new Length(size, LengthUnit.Px));
                image.SetValue(HeightProperty, new Length(size, LengthUnit.Px));
            }

            if (part.HasClass("value"))
                part.SetValue(ColorProperty, GetValue(ValueColorProperty));
        }
    }
}
