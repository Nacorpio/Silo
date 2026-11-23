using Silo;
using Silo.Layout;
using Silo.Rendering;
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

    [Fact]
    public void A_closable_tab_has_a_close_button_that_only_raises_the_event()
    {
        var tabs = new TabView();
        var requested = new List<int>();

        tabs.AddTab("One", new TextBlock { Text = "First" });
        tabs.AddTab("Two", new TextBlock { Text = "Second" }, closable: true);

        tabs.CloseRequested += (_, e) => requested.Add(e.Index);

        Click(tabs.GetHeader(1).Buttons[0]);

        Assert.Equal([1], requested);
        Assert.Equal(2, tabs.Count);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Empty(tabs.GetHeader(0).Buttons);
    }

    [Fact]
    public void Removing_the_selected_tab_selects_the_next_one_then_the_previous_one()
    {
        var tabs = Create();

        tabs.AddTab("Three", new TextBlock { Text = "Third" });
        tabs.SelectedIndex = 1;

        tabs.RemoveTab(1);

        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal(2, tabs.Count);
        Assert.Same(tabs.GetContent(1), tabs.SelectedContent);

        tabs.RemoveTab(1);

        Assert.Equal(0, tabs.SelectedIndex);

        tabs.RemoveTab(0);

        Assert.Equal(-1, tabs.SelectedIndex);
        Assert.Single(tabs.Children);
    }

    [Fact]
    public void Removing_a_tab_before_the_selected_one_keeps_the_same_tab_selected()
    {
        var tabs = Create();
        var raised = 0;

        tabs.SelectedIndex = 1;
        tabs.SelectionChanged += (_, _) => raised++;

        var selected = tabs.SelectedContent;

        tabs.RemoveTab(0);

        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Same(selected, tabs.SelectedContent);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Removing_a_tab_that_does_not_exist_is_rejected()
    {
        var tabs = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.RemoveTab(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => tabs.RemoveTab(-1));
    }

    [Fact]
    public void A_header_button_does_not_select_its_tab()
    {
        var tabs = Create();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """
            { "rules": {
                "TabStrip": { "flex-direction": "row" },
                "TabHeader": { "flex-direction": "row", "align-items": "center", "padding": "2px" },
                "TabHeaderButton": { "padding": "0 4px" }
            } }
            """;

        tabs.GetHeader(1).AddButton(new TabHeaderButton { Text = "x" });

        root.Children.Add(tabs);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));

        var button = tabs.GetHeader(1).Buttons[0];
        var (x, y) = (button.Bounds.X + 1, button.Bounds.Y + 1);
        var origin = tabs.GetHeader(1).Bounds;
        var strip = tabs.Strip.Bounds;
        var router = new PointerRouter(root);
        var clicked = false;

        button.Click += (_, _) => clicked = true;

        var px = tabs.Bounds.X + strip.X + origin.X + x;
        var py = tabs.Bounds.Y + strip.Y + origin.Y + y;

        router.Down(px, py);
        router.Up(px, py);

        Assert.True(clicked);
        Assert.Equal(0, tabs.SelectedIndex);
    }

    [Fact]
    public void The_buttons_go_after_the_title_unless_button_position_says_left()
    {
        var tabs = Create();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var button = new TabHeaderButton { Text = "x" };
        var sheet = """
            { "rules": { "TabHeader": { "button-position": "left" } } }
            """;

        tabs.GetHeader(0).AddButton(button);

        Assert.Same(button, tabs.GetHeader(0).Children[1]);

        root.Children.Add(tabs);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));

        Assert.Same(button, tabs.GetHeader(0).Children[0]);
    }

    [Fact]
    public void An_icon_adds_its_size_and_gap_to_the_width_of_the_header()
    {
        var plain = Create();
        var withIcon = Create();
        var sheet = """
            { "rules": { "TabHeader": { "icon-size": "10px", "icon-gap": "4px", "flex-direction": "row" } } }
            """;

        withIcon.GetHeader(0).Icon = new FakeImage(32, 32);

        var widths = new List<double>();

        foreach (var tabs in new[] { plain, withIcon })
        {
            var root = new Box { TextMeasurer = new FixedMeasurer() };

            root.Children.Add(tabs);
            root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
            root.Arrange(new Size(300, 200));

            widths.Add(tabs.GetHeader(0).Bounds.Width);
        }

        Assert.Equal(14, widths[1] - widths[0]);
    }

    [Fact]
    public void A_mask_icon_follows_the_text_color_of_the_selected_and_the_unselected_header()
    {
        var tabs = Create();
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var sheet = """
            { "rules": { "TabHeader": { "color": "#111111", "&:selected": { "color": "#ff0000" } } } }
            """;
        var builder = new DisplayListBuilder();

        tabs.GetHeader(0).Icon = new FakeMask(8, 8);
        tabs.GetHeader(1).Icon = new FakeMask(8, 8);

        root.Children.Add(tabs);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 200));
        root.Render(builder);

        var colors = builder.Build().Commands.OfType<DrawMaskCommand>().Select(command => command.Color).ToList();

        Assert.Equal([new Silo.Styling.Values.Color(255, 0, 0), new Silo.Styling.Values.Color(17, 17, 17)], colors);
    }

    sealed class FakeImage(int width, int height) : Silo.Rendering.IImage
    {
        public int Width { get; } = width;

        public int Height { get; } = height;
    }
}

public class TabHeaderTrimmingTests
{
    static List<string> DrawnTexts(string text, string rules)
    {
        var tabs = new TabView { Name = "t" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        tabs.AddTab(text, new TextBlock { Text = "x" });
        root.Children.Add(tabs);

        var sheet = $$"""{ "rules": { "Box": { "align-items": "start" }, "TabView": { "flex-direction": "column" }, "TabStrip": { "flex-direction": "row" }, "TabHeader": { "flex-direction": "row", "padding": "0 10px" {{rules}} } } }""";

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 200));

        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text)];
    }

    [Fact]
    public void A_header_text_that_fits_is_drawn_whole()
    {
        Assert.Contains("Short", DrawnTexts("Short", ""));
    }

    [Fact]
    public void A_text_too_long_for_the_max_width_is_cut_with_an_ellipsis()
    {
        var drawn = DrawnTexts("A very long tab title", """, "max-width": "100px" """);

        var trimmed = Assert.Single(drawn, t => t.EndsWith('…'));

        Assert.True(trimmed.Length * 7 <= 80);
        Assert.StartsWith("A very", trimmed);
    }
}
