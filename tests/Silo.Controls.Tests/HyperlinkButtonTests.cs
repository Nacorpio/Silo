using Silo;
using Xunit;

public class HyperlinkButtonTests
{
    [Fact]
    public void A_click_raises_navigate_with_the_uri()
    {
        var link = new HyperlinkButton { Text = "Docs", Uri = "https://example.com" };
        string? seen = null;

        link.Navigate += (_, e) => seen = e.Uri;
        link.PointerDown();
        link.PointerUp(inside: true);

        Assert.Equal("https://example.com", seen);
    }

    [Fact]
    public void A_release_outside_does_not_navigate()
    {
        var link = new HyperlinkButton { Uri = "x" };
        var raised = false;

        link.Navigate += (_, _) => raised = true;
        link.PointerDown();
        link.PointerUp(inside: false);

        Assert.False(raised);
    }

    [Fact]
    public void Visited_sets_the_pseudo_state()
    {
        var link = new HyperlinkButton();

        link.IsVisited = true;

        Assert.Contains("visited", link.PseudoStates);

        link.IsVisited = false;

        Assert.DoesNotContain("visited", link.PseudoStates);
    }
}
