namespace Silo.Studio;

/// <summary>
/// The list of categories beside the gallery. Each entry shows an icon, the name, how many Controls the category holds and a
/// short description. Pressing an entry, or the up and down arrows, shows that category in the <see cref="ThemeMatrix"/>.
/// </summary>
public sealed class CategoryList : Control
{
    /// <summary>The matrix that the list picks the category of.</summary>
    readonly ThemeMatrix matrix;

    /// <summary>The entries, by category.</summary>
    readonly Dictionary<string, CategoryEntry> entries = [];

    /// <summary>Creates the list for a matrix and keeps it in step with it.</summary>
    /// <param name="matrix">The matrix.</param>
    public CategoryList(ThemeMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        this.matrix = matrix;

        Name = "categories";

        foreach (var category in ThemeMatrix.Categories)
        {
            var entry = new CategoryEntry(category, ThemeMatrix.Describe(category));

            entry.Click += (_, _) => matrix.ShowCategory(category);

            entries[category] = entry;
            Children.Add(entry);
        }

        matrix.CategoryChanged += (_, _) => Sync();

        Sync();
    }

    /// <summary>Gets <c>Box</c>, so that the theme's Box Rules apply.</summary>
    public override string TypeName => "Box";

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the entry of a category.</summary>
    /// <param name="category">One of <see cref="ThemeMatrix.Categories"/>.</param>
    /// <returns>The entry.</returns>
    public Button Entry(string category) => entries[category];

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var all = ThemeMatrix.Categories.ToList();
        var index = all.IndexOf(matrix.SelectedCategory);

        var next = key switch
        {
            Key.Up => Math.Max(0, index - 1),
            Key.Down => Math.Min(all.Count - 1, index + 1),
            Key.Home => 0,
            Key.End => all.Count - 1,

            _ => -1,
        };

        return next >= 0 && matrix.ShowCategory(all[next]);
    }

    /// <summary>Marks the entry of the shown category as selected.</summary>
    void Sync()
    {
        foreach (var (category, entry) in entries)
            entry.IsSelected = category == matrix.SelectedCategory;
    }

    /// <summary>One entry of the list: a Button that holds an icon, the name with the count, and the description.</summary>
    sealed class CategoryEntry : Button
    {
        /// <summary>Runs before the first entry is created, so that the Styled properties are registered in time.</summary>
        static CategoryEntry()
        {
        }

        /// <summary>Creates the entry of a category.</summary>
        /// <param name="category">The category name.</param>
        /// <param name="info">The facts about it.</param>
        public CategoryEntry(string category, CategoryInfo info)
        {
            var text = new Box();
            var heading = new Box();

            AddClass("category");
            text.AddClass("category-text");
            heading.AddClass("category-heading");

            heading.Children.Add(Part(new TextBlock { Text = category }, "category-name"));
            heading.Children.Add(Part(new TextBlock { Text = info.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) }, "category-count"));

            text.Children.Add(heading);
            text.Children.Add(Part(new TextBlock { Text = info.Description }, "category-description"));

            Children.Add(new Icon { Glyph = info.Glyph });
            Children.Add(text);
        }

        /// <summary>Sets or clears the <c>selected</c> Pseudo-state.</summary>
        public bool IsSelected
        {
            set => SetPseudoState("selected", value);
        }

        /// <summary>Adds a Class to a Control.</summary>
        /// <param name="control">The Control.</param>
        /// <param name="name">The Class.</param>
        /// <returns>The same Control.</returns>
        static T Part<T>(T control, string name)
            where T : Control
        {
            control.AddClass(name);

            return control;
        }
    }
}
