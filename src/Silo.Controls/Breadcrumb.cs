namespace Silo;

/// <summary>A trail of the places that lead to the current one, each earlier place a link.</summary>
/// <remarks>
/// <para>
/// Every item but the last is a <see cref="HyperlinkButton"/> with the Class <c>crumb</c>; the last is the current page, a
/// TextBlock with the Class <c>current</c>. Between them sit <see cref="Icon"/>s with the Class <c>separator</c>, the
/// <c>chevron-right</c> glyph. Clicking an earlier item raises <see cref="ItemClicked"/> with its index; the Breadcrumb does not
/// change <see cref="Items"/> by itself.
/// </para>
/// <para>
/// A trail with more than <see cref="MaxItems"/> entries keeps the first item and the last ones, and shows a TextBlock with the
/// Class <c>ellipsis</c> (<c>…</c>) for those between. Apply styles again after the items change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var trail = new Breadcrumb { Items = ["Home", "Projects", "Silo", "Readme"] };
///
/// trail.ItemClicked += (_, index) => Navigate(trail.Items[index]);
/// </code>
/// </example>
public class Breadcrumb : Control
{
    /// <summary>The text that stands for the items that are left out.</summary>
    const string Ellipsis = "…";


    /// <summary>The items.</summary>
    IReadOnlyList<string> items = [];

    /// <summary>The most items that are shown.</summary>
    int maxItems = 5;


    /// <summary>
    /// Runs before the first Breadcrumb is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Breadcrumb()
    {
    }

    /// <summary>Raised when an item that is not the last was clicked. The argument is its index in <see cref="Items"/>.</summary>
    public event EventHandler<int>? ItemClicked;

    /// <summary>Gets or sets the texts of the trail, from the first place to the current one.</summary>
    public IReadOnlyList<string> Items
    {
        get => items;
        set
        {
            items = value ?? [];

            Rebuild();
        }
    }

    /// <summary>Gets or sets the most entries that are shown, the ellipsis counting as one, before the middle is left out. At least 3; the default is 5.</summary>
    public int MaxItems
    {
        get => maxItems;
        set
        {
            maxItems = Math.Max(3, value);

            Rebuild();
        }
    }

    /// <summary>Builds the items, the separators and the ellipsis.</summary>
    void Rebuild()
    {
        Children.Clear();

        var last = items.Count - 1;
        var hidden = items.Count > maxItems ? items.Count - (maxItems - 1) : 0;

        for (var i = 0; i <= last; i++)
        {
            if (hidden > 0 && i >= 1 && i <= hidden)
            {
                if (i == 1)
                {
                    AddSeparator();
                    AddEllipsis();
                }

                continue;
            }

            if (i > 0)
                AddSeparator();

            AddItem(i, i == last);
        }
    }

    /// <summary>Adds one item.</summary>
    /// <param name="index">The index in <see cref="Items"/>.</param>
    /// <param name="isCurrent">Whether it is the last item.</param>
    void AddItem(int index, bool isCurrent)
    {
        if (isCurrent)
        {
            var current = new TextBlock { Text = items[index] };

            current.AddClass("current");
            Children.Add(current);

            return;
        }

        var crumb = new HyperlinkButton { Text = items[index] };

        crumb.AddClass("crumb");
        crumb.Click += (_, _) => ItemClicked?.Invoke(this, index);

        Children.Add(crumb);
    }

    /// <summary>Adds the glyph between two items.</summary>
    void AddSeparator()
    {
        var separator = new Icon { Glyph = "chevron-right" };

        separator.AddClass("separator");
        Children.Add(separator);
    }

    /// <summary>Adds the mark for the items that are left out.</summary>
    void AddEllipsis()
    {
        var ellipsis = new TextBlock { Text = Ellipsis };

        ellipsis.AddClass("ellipsis");
        Children.Add(ellipsis);
    }
}
