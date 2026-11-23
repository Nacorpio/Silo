using Silo;
using Xunit;

public class BreadcrumbTests
{
    static List<string> Describe(Breadcrumb bar) => [.. bar.Children.Select(c => c switch
    {
        HyperlinkButton link => $"link:{link.Text}",
        Icon => "sep",
        TextBlock text => $"{(text.Classes.Contains("current") ? "current" : "text")}:{text.Text}",
        _ => "?",
    })];

    [Fact]
    public void Earlier_items_are_links_and_the_last_is_the_current_page()
    {
        var bar = new Breadcrumb { Items = ["Home", "Docs", "Readme"] };

        Assert.Equal(["link:Home", "sep", "link:Docs", "sep", "current:Readme"], Describe(bar));
    }

    [Fact]
    public void Clicking_a_link_raises_the_event_with_its_index()
    {
        var bar = new Breadcrumb { Items = ["Home", "Docs", "Readme"] };
        int? clicked = null;

        bar.ItemClicked += (_, index) => clicked = index;

        var docs = (Button)bar.Children[2];

        docs.PointerDown();
        docs.PointerUp(inside: true);

        Assert.Equal(1, clicked);
    }

    [Fact]
    public void A_long_trail_keeps_the_first_and_last_items_and_shows_an_ellipsis()
    {
        var bar = new Breadcrumb { Items = ["A", "B", "C", "D", "E", "F", "G"], MaxItems = 5 };

        Assert.Equal(["link:A", "sep", "text:…", "sep", "link:E", "sep", "link:F", "sep", "current:G"], Describe(bar));
    }

    [Fact]
    public void The_last_shown_items_keep_their_own_indices()
    {
        var bar = new Breadcrumb { Items = ["A", "B", "C", "D", "E", "F", "G"], MaxItems = 5 };
        int? clicked = null;

        bar.ItemClicked += (_, index) => clicked = index;

        var e = (Button)bar.Children[4];

        e.PointerDown();
        e.PointerUp(inside: true);

        Assert.Equal(4, clicked);
    }

    [Fact]
    public void A_trail_that_fits_is_shown_whole_and_an_empty_one_is_empty()
    {
        Assert.Equal(9, new Breadcrumb { Items = ["A", "B", "C", "D", "E"] }.Children.Count);
        Assert.Empty(new Breadcrumb().Children);
    }
}
