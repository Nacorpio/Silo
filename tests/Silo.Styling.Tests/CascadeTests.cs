using Silo.Styling;
using Xunit;

public class CascadeTests
{
    static Stylesheet Sheet(string json) => JsoncStylesheetReader.Read(json, "t.jsonc");

    [Fact]
    public void Higher_specificity_beats_later_source_order()
    {
        var resolver = new StyleResolver([Sheet("""{ "rules": { "Button.primary": { "color": "red" }, "Button": { "color": "blue" } } }""")]);
        var cascaded = resolver.Cascade(new TestNode("Button", classes: ["primary"]));

        Assert.Equal("red", cascaded["color"].Value);
    }

    [Fact]
    public void Later_rule_wins_when_specificity_ties_and_later_sheet_wins_across_sheets()
    {
        var resolver = new StyleResolver(
        [
            Sheet("""{ "rules": { "Button": { "color": "red" }, "Button ": { "color": "green" } } }"""),
            Sheet("""{ "rules": { "Button": { "color": "blue" } } }"""),
        ]);

        var cascaded = resolver.Cascade(new TestNode("Button"));

        Assert.Equal("blue", cascaded["color"].Value);
    }

    [Fact]
    public void Pseudo_state_rules_apply_only_while_the_state_is_set()
    {
        var resolver = new StyleResolver([Sheet("""{ "rules": { "Button": { "color": "red", "&:hover": { "color": "blue" } } } }""")]);

        Assert.Equal("red", resolver.Cascade(new TestNode("Button"))["color"].Value);
        Assert.Equal("blue", resolver.Cascade(new TestNode("Button", states: ["hover"]))["color"].Value);
    }

    [Fact]
    public void Part_declarations_are_separate_from_the_element_declarations()
    {
        var resolver = new StyleResolver([Sheet("""{ "rules": { "Button": { "color": "red", "&::part(icon)": { "margin-right": "6px" } } } }""")]);
        var node = new TestNode("Button");

        Assert.False(resolver.Cascade(node).ContainsKey("margin-right"));
        Assert.Equal("6px", resolver.Cascade(node, "icon")["margin-right"].Value);
    }

    [Fact]
    public void Cascaded_value_remembers_its_source_location()
    {
        var resolver = new StyleResolver([Sheet("{\n \"rules\": {\n \"Button\": { \"color\": \"red\" } } }")]);
        var cascaded = resolver.Cascade(new TestNode("Button"));

        Assert.Equal(3, cascaded["color"].Source.Span.Line);
    }

    [Fact]
    public void Tokens_inherit_down_the_tree_and_rules_can_redeclare_them()
    {
        var resolver = new StyleResolver([Sheet("""{ "tokens": { "--accent": "blue" }, "rules": { ".danger": { "--accent": "red" } } }""")]);
        var parent = new TestNode("Box", classes: ["danger"]);
        var child = new TestNode("Button", parent: parent);

        Assert.Equal("blue", resolver.ResolveTokens(new TestNode("Button"))["--accent"]);
        Assert.Equal("red", resolver.ResolveTokens(child)["--accent"]);
    }

    [Fact]
    public void Compute_substitutes_tokens_fallbacks_and_nested_references()
    {
        var tokens = new Dictionary<string, string> { ["--a"] = "var(--b)", ["--b"] = "4px" };

        Assert.Equal("4px 8px", StyleResolver.Compute("var(--a) var(--missing, 8px)", tokens));
    }

    [Fact]
    public void Compute_rejects_undefined_tokens_and_cycles()
    {
        var cycle = new Dictionary<string, string> { ["--a"] = "var(--b)", ["--b"] = "var(--a)" };

        Assert.Throws<StylesheetException>(() => StyleResolver.Compute("var(--missing)", cycle));
        Assert.Throws<StylesheetException>(() => StyleResolver.Compute("var(--a)", cycle));
    }
}
