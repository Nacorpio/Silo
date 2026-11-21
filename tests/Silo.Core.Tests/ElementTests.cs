using Silo;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class ElementTests
{
    static StyleResolver Resolver(string json) => new([JsoncStylesheetReader.Read(json, "t.jsonc")]);

    [Fact]
    public void Adding_a_child_sets_its_parent_and_removing_clears_it()
    {
        var root = new Element();
        var child = new Element();

        root.Children.Add(child);

        Assert.Same(root, child.Parent);
        Assert.Single(root.Children);

        Assert.True(root.Children.Remove(child));
        Assert.Null(child.Parent);
    }

    [Fact]
    public void A_child_cannot_have_two_parents_or_become_its_own_ancestor()
    {
        var root = new Element();
        var child = new Element();
        var grandChild = new Element();

        root.Children.Add(child);
        child.Children.Add(grandChild);

        Assert.Throws<InvalidOperationException>(() => new Element().Children.Add(child));
        Assert.Throws<InvalidOperationException>(() => grandChild.Children.Add(root));
    }

    [Fact]
    public void Classes_and_pseudo_states_take_part_in_selector_matching()
    {
        var probe = new Probe { Name = "save" };

        probe.AddClass("primary");

        var selector = Selector.Parse("Probe.primary#save:hover");

        Assert.False(selector.Matches(probe));

        probe.SetHover(true);

        Assert.True(selector.Matches(probe));
    }

    [Fact]
    public void Value_precedence_is_local_then_styled_then_default()
    {
        var probe = new Probe();

        Assert.Equal(default, probe.Background);

        probe.ApplyStyles(Resolver("""{ "rules": { "Probe": { "background": "red" } } }"""));

        Assert.Equal(new Color(255, 0, 0), probe.Background);

        probe.Background = new Color(0, 0, 255);

        Assert.Equal(new Color(0, 0, 255), probe.Background);

        probe.ClearValue(Probe.BackgroundProperty);

        Assert.Equal(new Color(255, 0, 0), probe.Background);
    }

    [Fact]
    public void Tokens_resolve_while_styles_apply()
    {
        var probe = new Probe();

        probe.ApplyStyles(Resolver("""{ "tokens": { "--accent": "#00ff00" }, "rules": { "Probe": { "background": "var(--accent)" } } }"""));

        Assert.Equal(new Color(0, 255, 0), probe.Background);
    }

    [Fact]
    public void Pseudo_state_styles_apply_after_the_state_changes_and_reapplying()
    {
        var probe = new Probe();
        var resolver = Resolver("""{ "rules": { "Probe": { "background": "red", "&:hover": { "background": "blue" } } } }""");

        probe.ApplyStyles(resolver);

        Assert.Equal(new Color(255, 0, 0), probe.Background);

        probe.SetHover(true);
        probe.ApplyStyles(resolver);

        Assert.Equal(new Color(0, 0, 255), probe.Background);
    }

    [Fact]
    public void Inheriting_properties_flow_from_the_parent_and_others_do_not()
    {
        var root = new Probe();
        var child = new Probe();

        root.Children.Add(child);

        root.Name = "root";
        root.ApplyStyles(Resolver("""{ "rules": { "#root": { "color": "red", "background": "red" } } }"""));

        Assert.Equal(new Color(255, 0, 0), child.GetValue(Probe.ForegroundProperty));
        Assert.Equal(default, child.Background);
    }

    [Fact]
    public void Invalid_declarations_are_reported_and_skipped()
    {
        var probe = new Probe();

        var diagnostics = probe.ApplyStyles(Resolver("""{ "rules": { "Probe": { "background": "notacolor", "bogus": "1", "color": "var(--missing)", "opacity": "50%" } } }"""));

        Assert.Equal(3, diagnostics.Count);
        Assert.Contains(diagnostics, d => d.Property == "background");
        Assert.Contains(diagnostics, d => d.Property == "bogus");
        Assert.Contains(diagnostics, d => d.Property == "color");
        Assert.Equal(0.5, probe.GetValue(Element.OpacityProperty));
    }

    [Fact]
    public void Property_changes_are_reported_once_and_only_when_the_value_differs()
    {
        var probe = new Probe();

        probe.Background = new Color(1, 2, 3);
        probe.Background = new Color(1, 2, 3);

        Assert.Equal([Probe.BackgroundProperty], probe.Changes);
    }

    [Fact]
    public void Registering_the_same_name_twice_on_one_owner_throws()
    {
        Assert.Throws<ArgumentException>(() => StyledProperty.Register<Probe, double>("background", 0, double.TryParse));
    }

    [Fact]
    public void Properties_are_found_through_base_types()
    {
        _ = new Probe();

        Assert.Same(Element.OpacityProperty, StyledProperty.Find(typeof(Probe), "opacity"));
        Assert.Same(Probe.BackgroundProperty, StyledProperty.Find(typeof(Probe), "background"));
        Assert.Null(StyledProperty.Find(typeof(Element), "background"));
    }
}
