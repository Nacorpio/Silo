using Silo;
using Silo.Studio;
using Xunit;

public class ControlCatalogTests
{
    /// <summary>Public Control types that have no sample of their own, with the reason.</summary>
    static readonly Dictionary<string, string> Excluded = new()
    {
        ["GalleryCell"] = "Made by a GalleryView for the items that are on screen, so it only exists after layout.",
        ["ToastHost"] = "Made by the window scene; the Toast samples show what it holds.",
        ["Window"] = "The root of an app, not something a theme styles inside a page.",
    };

    static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;

        foreach (var child in root.Children.OfType<Control>())
            foreach (var inner in Descendants(child))
                yield return inner;
    }

    static IEnumerable<Type> ControlTypes() =>
        new[] { typeof(Control).Assembly, typeof(QuantityBox).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Control).IsAssignableFrom(t) && t.IsPublic && !t.IsAbstract);

    [Fact]
    public void Every_public_Control_appears_in_a_sample_or_is_excluded_with_a_reason()
    {
        var shown = ControlCatalog.Entries.SelectMany(e => Descendants(e.Create())).Select(c => c.GetType()).ToHashSet();
        var missing = ControlTypes().Where(t => !shown.Contains(t) && !Excluded.ContainsKey(t.Name)).Select(t => t.Name).Order().ToList();

        Assert.Equal([], missing);
    }

    [Fact]
    public void The_exclusion_list_only_names_Controls_that_exist_and_have_no_sample()
    {
        var shown = ControlCatalog.Entries.SelectMany(e => Descendants(e.Create())).Select(c => c.GetType().Name).ToHashSet();
        var types = ControlTypes().Select(t => t.Name).ToHashSet();

        Assert.All(Excluded.Keys, name => Assert.Contains(name, types));
        Assert.All(Excluded.Keys, name => Assert.DoesNotContain(name, shown));
    }

    [Fact]
    public void Every_state_a_Control_declares_is_one_that_exists()
    {
        string[] known = ["active", "checked", "expanded", "focus", "focus-within", "hover", "open", "selected", "visited"];

        var declared = ControlTypes().Append(typeof(Control)).Append(typeof(ToggleButton))
            .SelectMany(t => t.GetCustomAttributes(typeof(PseudoStatesAttribute), inherit: false).Cast<PseudoStatesAttribute>())
            .SelectMany(a => a.States)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.All(declared, state => Assert.Contains(state, known));
    }

    [Fact]
    public void A_row_has_a_cell_for_each_state_its_Control_declares_and_a_normal_one()
    {
        var matrix = new ThemeMatrix();

        foreach (var entry in ControlCatalog.Entries)
        {
            Assert.Contains("normal", entry.States);

            foreach (var column in ThemeMatrix.States)
                Assert.Equal(entry.States.Contains(column), matrix.Cell(entry.Name, column) is not null);
        }
    }

    [Fact]
    public void The_states_of_a_Control_come_from_its_declaration_and_a_subclass_replaces_it()
    {
        Assert.Equal(["normal", "hover", "pressed", "focus"], ControlCatalog.StatesOf(typeof(Button)));
        Assert.Equal(["normal", "hover", "pressed", "focus", "checked"], ControlCatalog.StatesOf(typeof(CheckBox)));
        Assert.Equal(["normal", "hover", "pressed"], ControlCatalog.StatesOf(typeof(RepeatButton)));
        Assert.Equal(["normal", "hover", "focus"], ControlCatalog.StatesOf(typeof(PasswordBox)));
        Assert.Equal(["normal"], ControlCatalog.StatesOf(typeof(ProgressBar)));
    }
}
