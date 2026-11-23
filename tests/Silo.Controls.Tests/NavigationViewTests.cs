using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class NavigationViewTests
{
    const string Sheet = """
        { "rules": {
          "Box": { "align-items": "start" },
          "NavigationView": { "flex-direction": "row" },
          "NavigationView .pane": { "flex-direction": "column", "width": "120px" },
          "NavigationView .item": { "padding": "4px 4px 4px 30px" }
        } }
        """;

    static NavigationView Create()
    {
        var nav = new NavigationView
        {
            Items = [new("Home", "home"), new("Library", IsHeader: true), new("Search", "search"), new("Settings", "menu")],
        };

        return nav;
    }

    static Box Arrange(NavigationView nav)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(nav);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        return root;
    }

    static Control Pane(NavigationView nav) => (Control)nav.Children[0];

    [Fact]
    public void The_pane_lists_headers_and_destinations_in_order()
    {
        var pane = Pane(Create());

        Assert.Equal(4, pane.Children.Count);
        Assert.Contains("item", ((Control)pane.Children[0]).Classes);
        Assert.Contains("header", ((Control)pane.Children[1]).Classes);
    }

    [Fact]
    public void Clicking_a_destination_selects_it_by_its_index_in_items()
    {
        var nav = Create();
        var raised = 0;

        nav.SelectionChanged += (_, _) => raised++;

        var search = (Button)Pane(nav).Children[2];

        search.PointerDown();
        search.PointerUp(inside: true);

        Assert.Equal(2, nav.SelectedIndex);
        Assert.Equal("Search", nav.SelectedItem!.Text);
        Assert.Equal(1, raised);
        Assert.Contains("selected", search.Classes);
    }

    [Fact]
    public void A_header_or_a_bad_index_selects_nothing_and_a_new_selection_unmarks_the_old()
    {
        var nav = Create();

        nav.SelectedIndex = 0;
        nav.SelectedIndex = 2;

        Assert.DoesNotContain("selected", ((Control)Pane(nav).Children[0]).Classes);

        nav.SelectedIndex = 1;

        Assert.Equal(-1, nav.SelectedIndex);
        Assert.Null(nav.SelectedItem);
    }

    [Fact]
    public void The_content_sits_beside_the_pane()
    {
        var nav = Create();
        var page = new TextBlock { Text = "Page" };

        nav.Content = page;

        Arrange(nav);

        Assert.True(((Control)nav.Children[1]).Bounds.X >= Pane(nav).Bounds.X + Pane(nav).Bounds.Width);
        Assert.Same(page, ((Control)nav.Children[1]).Children[0]);

        nav.Content = null;

        Assert.Empty(((Control)nav.Children[1]).Children);
    }

    [Fact]
    public void A_closed_pane_is_left_out()
    {
        var nav = Create();

        nav.IsPaneOpen = false;

        Assert.Single(nav.Children);

        nav.IsPaneOpen = true;

        Assert.Equal(2, nav.Children.Count);
    }

    [Fact]
    public void A_compact_pane_drops_headers_and_text_and_keeps_the_text_as_a_tooltip()
    {
        var nav = Create();

        nav.IsCompact = true;

        var pane = Pane(nav);
        var home = (Button)pane.Children[0];

        Assert.Equal(3, pane.Children.Count);
        Assert.Equal(string.Empty, home.Text);
        Assert.Equal("Home", home.Tooltip);
        Assert.Contains("compact", nav.Classes);
    }

    [Fact]
    public void The_pane_width_can_be_set_in_code()
    {
        var nav = Create();

        nav.PaneWidth = 90;

        Arrange(nav);

        Assert.Equal(90, Pane(nav).Bounds.Width);
    }

    [Fact]
    public void A_destination_draws_its_glyph()
    {
        var nav = Create();
        var root = Arrange(nav);
        var builder = new DisplayListBuilder();

        root.Render(builder);

        Assert.NotEmpty(builder.Build().Commands.OfType<StrokePolylineCommand>());
    }
}
