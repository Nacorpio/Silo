using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class TabViewTests
{
    static TabView Create()
    {
        var tabs = new TabView();

        tabs.AddTab("One", new TextBlock { Text = "First" });
        tabs.AddTab("Two", new TextBlock { Text = "Second" });

        return tabs;
    }

    static void Click(Button button)
    {
        button.PointerDown();
        button.PointerUp(inside: true);
    }

    [Fact]
    public void The_first_tab_is_selected_and_only_its_content_is_in_the_tree()
    {
        var tabs = Create();

        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(2, tabs.Count);
        Assert.Equal(2, tabs.Children.Count);
        Assert.Same(tabs.Strip, tabs.Children[0]);
        Assert.Same(tabs.GetContent(0), tabs.Children[1]);
        Assert.Contains("selected", tabs.GetHeader(0).PseudoStates);
        Assert.DoesNotContain("selected", tabs.GetHeader(1).PseudoStates);
    }

    [Fact]
    public void Clicking_a_header_selects_its_tab_and_raises_the_event()
    {
        var tabs = Create();
        var raised = 0;

        tabs.SelectionChanged += (_, _) => raised++;

        Click(tabs.GetHeader(1));

        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal(1, raised);
        Assert.Same(tabs.GetContent(1), tabs.Children[1]);
        Assert.DoesNotContain(tabs.GetContent(0), tabs.Children);
        Assert.Contains("selected", tabs.GetHeader(1).PseudoStates);
        Assert.DoesNotContain("selected", tabs.GetHeader(0).PseudoStates);
    }

    [Fact]
    public void Selecting_the_selected_tab_again_does_not_raise_the_event()
    {
        var tabs = Create();
        var raised = 0;

        tabs.SelectionChanged += (_, _) => raised++;
        tabs.SelectedIndex = 0;

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Clearing_the_selection_removes_the_content()
    {
        var tabs = Create();

        tabs.SelectedIndex = -1;

        Assert.Null(tabs.SelectedContent);
        Assert.Single(tabs.Children);
    }

    [Fact]
    public void An_index_outside_the_tabs_is_rejected()
    {
        var tabs = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.SelectedIndex = 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.SelectedIndex = -2);
    }

    [Fact]
    public void The_selected_header_is_styled_with_the_selected_Pseudo_state()
    {
        var tabs = Create();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """
            { "rules": { "TabHeader": { "color": "#111111", "&:selected": { "color": "#ff0000" } } } }
            """;
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]);

        root.Children.Add(tabs);
        root.ApplyStyles(resolver);

        var first = tabs.GetHeader(0).GetValue(TextControl.ColorProperty);
        var second = tabs.GetHeader(1).GetValue(TextControl.ColorProperty);

        Assert.NotEqual(first, second);
    }
}
