using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

/// <summary>One Control shown in Silo Studio's theme matrix: a name, a category and a way to make a sample of it.</summary>
/// <remarks>
/// The states that apply are not listed by hand. They come from the <see cref="PseudoStatesAttribute"/> of the sample's type,
/// so a Control that learns a new Pseudo-state gets a new cell without anyone editing the catalog.
/// </remarks>
/// <param name="name">The row label, which is the type name that Selectors use.</param>
/// <param name="category">The name of the tab page that holds the row.</param>
/// <param name="create">Creates a new sample Control.</param>
public sealed class CatalogEntry(string name, string category, Func<Control> create)
{
    /// <summary>The states that apply, found the first time they are asked for.</summary>
    IReadOnlyList<string>? states;

    /// <summary>Gets the row label, which is the type name that Selectors use.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the name of the tab page that holds the row.</summary>
    public string Category { get; } = category;

    /// <summary>Gets the states that apply, as column names from <see cref="ControlCatalog.Columns"/>. <c>normal</c> always does.</summary>
    public IReadOnlyList<string> States => states ??= ControlCatalog.StatesOf(Create().GetType());

    /// <summary>Creates a new sample Control.</summary>
    /// <returns>The Control.</returns>
    public Control Create() => create();
}

/// <summary>
/// Every Control of the library with a sample of it, grouped in categories. The theme matrix shows it and the tests check that it
/// is complete.
/// </summary>
/// <remarks>
/// The samples of a category are in <c>ControlCatalog.&lt;Category&gt;.cs</c>. To add a Control, add an entry there and give the
/// Control class a <see cref="PseudoStatesAttribute"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// foreach (var entry in ControlCatalog.Entries.Where(e => e.Category == "Buttons"))
///     Console.WriteLine($"{entry.Name}: {string.Join(", ", entry.States)}");
/// </code>
/// </example>
public static partial class ControlCatalog
{
    /// <summary>The columns of the matrix, in order, with <c>normal</c> for none. They are the states that the matrix can show.</summary>
    public static readonly IReadOnlyList<string> Columns = ["normal", "hover", "pressed", "focus", "checked"];

    /// <summary>The Pseudo-state that each column other than <c>normal</c> sets.</summary>
    static readonly Dictionary<string, string> ColumnStates = new()
    {
        ["hover"] = "hover",
        ["pressed"] = "active",
        ["focus"] = "focus",
        ["checked"] = "checked",
    };

    /// <summary>The glyph and the description of each category.</summary>
    static readonly Dictionary<string, (string Glyph, string Description)> Details = new()
    {
        ["Buttons"] = ("check", "Buttons, toggles and radios"),
        ["Text input"] = ("pencil", "Text boxes and search"),
        ["Values and lists"] = ("list", "Sliders, numbers and lists"),
        ["Inputs"] = ("sliders", "Segments, ratings, token boxes"),
        ["Data"] = ("grid", "Grids and trees"),
        ["Dates"] = ("calendar", "Calendar and date picker"),
        ["Colors"] = ("eye", "Color panel and picker"),
        ["Display"] = ("info", "Labels, badges, avatars, stats"),
        ["Panels"] = ("layout", "Cards, groups and expanders"),
        ["Navigation"] = ("arrow-right", "Breadcrumbs, pagers, steppers"),
        ["Shell"] = ("home", "Status bars and navigation panes"),
        ["Collections"] = ("image", "Thumbnail and card galleries"),
        ["Layout"] = ("layout", "Separators and containers"),
        ["Menus and dialogs"] = ("menu", "Menus, dialogs, tooltips, toasts"),
    };

    /// <summary>The icon that <see cref="Dot"/> creates once.</summary>
    static SkiaImage? dot;

    /// <summary>Gets every entry, grouped by category in the order of <see cref="Categories"/>.</summary>
    public static IReadOnlyList<CatalogEntry> Entries { get; } =
    [
        .. Buttons(),
        .. TextInput(),
        .. ValuesAndLists(),
        .. Inputs(),
        .. Data(),
        .. Dates(),
        .. Colors(),
        .. Display(),
        .. Panels(),
        .. Navigation(),
        .. Shell(),
        .. Collections(),
        .. Layout(),
        .. MenusAndDialogs(),
    ];

    /// <summary>Gets the names of the categories, in order. Each holds the entries of one group of Controls.</summary>
    public static IEnumerable<string> Categories => Entries.Select(e => e.Category).Distinct();

    /// <summary>Gets the glyph, the short description and the number of Controls of a category.</summary>
    /// <param name="category">One of <see cref="Categories"/>.</param>
    /// <returns>The facts about the category.</returns>
    public static CategoryInfo Describe(string category)
    {
        var (glyph, description) = Details.GetValueOrDefault(category, ("info", string.Empty));

        return new CategoryInfo(glyph, description, Entries.Count(e => e.Category == category));
    }

    /// <summary>Finds the matrix columns that apply to a Control type.</summary>
    /// <param name="type">The type of a sample Control.</param>
    /// <returns>The columns, in order. <c>normal</c> is always one of them.</returns>
    public static IReadOnlyList<string> StatesOf(Type type)
    {
        var declared = (Attribute.GetCustomAttribute(type, typeof(PseudoStatesAttribute), inherit: true) as PseudoStatesAttribute)?.States ?? [];

        return [.. Columns.Where(c => c == "normal" || (ColumnStates.TryGetValue(c, out var state) && declared.Contains(state)))];
    }

    /// <summary>Gets a small round mask icon, which takes the text color of the Control that shows it.</summary>
    static SkiaImage Dot => dot ??= SkiaImage.Create(
        16,
        16,
        canvas => canvas.DrawCircle(8, 8, 5, new SKPaint { Color = SKColors.Black, IsAntialias = true }),
        asMask: true);
}
